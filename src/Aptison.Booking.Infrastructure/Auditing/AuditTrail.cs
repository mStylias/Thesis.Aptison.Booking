using System.Text.Json;
using Aptison.Booking.Application.Abstractions.Auditing;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.System;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Infrastructure.Auditing;

/// <summary>
/// The owner is left to the ownership interceptor, which stamps the request's company. The address
/// is the caller's even behind the proxy: <c>UseRealClientAddress</c> has already rewritten it.
/// </summary>
internal sealed class AuditTrail(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentAppUser currentAppUser,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : IAuditTrail
{
    private static readonly JsonSerializerOptions DataFormat = new(JsonSerializerDefaults.Web);

    public async Task RecordAsync(
        string action, string entityType, Guid entityId, object? data, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        await unitOfWork.GetRepository<AuditLog>().AddAsync(
            new AuditLog
            {
                Id = Guid.CreateVersion7(),
                OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
                ActorId = currentAppUser.UserId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = httpContext?.Connection.RemoteIpAddress,
                UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent,
                Data = data is null ? null : JsonSerializer.SerializeToDocument(data, DataFormat)
            },
            cancellationToken);
    }
}
