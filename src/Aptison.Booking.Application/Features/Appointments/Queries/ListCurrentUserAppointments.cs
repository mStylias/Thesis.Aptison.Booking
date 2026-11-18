using Aptison.Booking.Application.Abstractions.Storage;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Appointments.Common.Enums;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>
/// The caller's appointments at every shop, booked online or by a shop for them. Upcoming soonest
/// first, past most recent first.
/// </summary>
public class ListCurrentUserAppointmentsRequest : IQuery<Result<AppointmentPageResponse, HttpError>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    public AppointmentPeriod Period { get; set; }

    /// <summary>From 1. Out-of-range values are clamped rather than refused.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    /// <inheritdoc cref="GetCurrentUserAppointmentRequest.LanguageCode"/>
    public string? LanguageCode { get; set; }
}

public class ListCurrentUserAppointmentsHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    TimeProvider timeProvider,
    IOptions<FileStorageOptions> fileStorageOptions)
    : IQueryHandler<ListCurrentUserAppointmentsRequest, Result<AppointmentPageResponse, HttpError>>
{
    public async Task<Result<AppointmentPageResponse, HttpError>> HandleAsync(
        ListCurrentUserAppointmentsRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = Math.Clamp(
            request.PageSize ?? ListCurrentUserAppointmentsRequest.DefaultPageSize,
            1,
            ListCurrentUserAppointmentsRequest.MaxPageSize);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        // One past the page, to learn whether another follows without counting the whole history.
        Func<IQueryable<Appointment>, IQueryable<Appointment>> shape = request.Period == AppointmentPeriod.Upcoming
            ? query => query
                .Where(appointment => activeStatuses.Contains(appointment.Status) && appointment.StartsAt > utcNow)
                .OrderBy(appointment => appointment.StartsAt)
                .ThenBy(appointment => appointment.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize + 1)
            : query => query
                .Where(appointment => !activeStatuses.Contains(appointment.Status) || appointment.StartsAt <= utcNow)
                .OrderByDescending(appointment => appointment.StartsAt)
                .ThenByDescending(appointment => appointment.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize + 1);

        var read = await Result.TryAsync(
            () => CustomerAppointmentReader.ReadAsync(
                unitOfWork, userContext, request.CustomerId, shape, request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl, utcNow, cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch your appointments.", errorCode: ErrorCodes.Appointments.ListFetchFailed));

        if (read.IsError)
        {
            return read.Error!;
        }

        var appointments = read.Data!;

        return new AppointmentPageResponse(
            [.. appointments.Take(pageSize)], page, pageSize, appointments.Count > pageSize);
    }
}
