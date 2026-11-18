using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// The drawer's read, which every management write also answers with. No company filter here: the
/// ownership filter makes another shop's appointment read as absent.
/// </summary>
internal static class AppointmentDetailsReader
{
    public static async Task<AppointmentDetailsResponse?> ReadAsync(
        IRepository<Appointment, BookingContext> appointmentRepo,
        IRepository<Language, BookingContext> languageRepo,
        Guid appointmentId,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(languageRepo, languageCode, cancellationToken);

        return await appointmentRepo
            .QueryDto<AppointmentDetailsResponse>(
                query => query.Where(row => row.Id == appointmentId),
                new MapParameters { ["languageId"] = languageId })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static HttpError CreateNotFoundError() =>
        HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound);
}
