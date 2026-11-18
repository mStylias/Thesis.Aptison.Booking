using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Commands;

/// <summary>
/// Turns push off for the caller's current browser, by its endpoint. A browser that is not the
/// caller's, or not registered at all, is already off, so both succeed.
/// </summary>
public class UnregisterPushDeviceRequest : ICommand<Result<HttpError>>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = string.Empty;
}

public class UnregisterPushDeviceHandler(IUnitOfWork<BookingContext> unitOfWork)
    : ICommandHandler<UnregisterPushDeviceRequest, Result<HttpError>>
{
    public async Task<Result<HttpError>> HandleAsync(
        UnregisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        var endpoint = request.Endpoint.Trim();

        return await Result.TryAsync(
            async () =>
            {
                await unitOfWork.GetRepository<UserDevice>().ExecuteHardDeleteAsync(
                    query => query.Where(row =>
                        row.UserId == request.UserId
                        && row.Platform == UserDevicePlatform.Web
                        && row.PushData.RootElement.GetProperty(PushDeviceData.EndpointProperty).GetString() == endpoint),
                    cancellationToken);
            },
            _ => HttpError.Internal(
                "The device could not be removed. Please try again.", errorCode: ErrorCodes.Reminders.DeviceWriteFailed));
    }
}
