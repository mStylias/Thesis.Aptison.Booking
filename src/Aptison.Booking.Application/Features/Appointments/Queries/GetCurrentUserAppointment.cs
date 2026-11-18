using Aptison.Booking.Application.Abstractions.Storage;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>
/// One of the caller's own appointments, in the shape <c>POST /appointments</c> answered with. The
/// success screen is its own route, so a refresh reads the booking back.
/// </summary>
public class GetCurrentUserAppointmentRequest : IQuery<Result<AppointmentResponse, HttpError>>
{
    /// <summary>From the token. <c>JsonIgnore</c> so a query naming someone else is unreachable.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    public Guid Id { get; set; }

    /// <summary>The language of the shop and service names. Falls back to whichever each row has.</summary>
    public string? LanguageCode { get; set; }
}

public class GetCurrentUserAppointmentHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    TimeProvider timeProvider,
    IOptions<FileStorageOptions> fileStorageOptions)
    : IQueryHandler<GetCurrentUserAppointmentRequest, Result<AppointmentResponse, HttpError>>
{
    public async Task<Result<AppointmentResponse, HttpError>> HandleAsync(
        GetCurrentUserAppointmentRequest request, CancellationToken cancellationToken)
    {
        var appointments = await CustomerAppointmentReader.ReadAsync(
            unitOfWork,
            userContext,
            request.CustomerId,
            query => query.Where(appointment => appointment.Id == request.Id),
            request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        // Someone else's appointment answers the same way: a 403 would confirm the id exists.
        return appointments.Count == 0
            ? HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound)
            : appointments[0];
    }
}
