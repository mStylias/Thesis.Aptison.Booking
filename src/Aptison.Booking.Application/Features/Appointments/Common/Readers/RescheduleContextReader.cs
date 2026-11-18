using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// Reads one of the customer's appointments for a move, or the refusal: someone else's or missing,
/// a shop no longer taking bookings, or past the cutoff. Callers read under <c>BecomeAllOwners</c>.
/// </summary>
internal static class RescheduleContextReader
{
    public static async Task<Result<RescheduleContext, HttpError>> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid customerId,
        Guid appointmentId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var appointment = await unitOfWork.GetRepository<Appointment>()
            .QuerySelect(
                row => new { row.Id, row.OwnerId, row.StaffMemberId, row.StartsAt, row.BlockedFrom, row.BlockedUntil, row.Status },
                query => query.Where(row => row.Id == appointmentId && row.CustomerId == customerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound);
        }

        var company = await BookingCompanyReader.ReadByIdAsync(
            unitOfWork.GetRepository<Company>(), appointment.OwnerId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound(
                "This barbershop is not taking bookings.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var refusal = CustomerAppointmentRules.EnsureChangeable(
            appointment.Status, appointment.StartsAt, company.CancellationCutoffMinutes, utcNow, isMove: true);

        if (refusal.IsError)
        {
            return refusal.Error!;
        }

        // Ordered by id, a v7 guid: the order the booking inserted them in.
        var lines = await unitOfWork.GetRepository<AppointmentService>()
            .QueryDto<CandidateOffering>(query => query.Where(line => line.AppointmentId == appointment.Id).OrderBy(line => line.Id))
            .ToListAsync(cancellationToken);

        return new RescheduleContext(
            company,
            BookingCompanyReader.GetTimeZone(company.Timezone),
            new MovedAppointment(appointment.Id, appointment.StaffMemberId, appointment.BlockedFrom, appointment.BlockedUntil),
            new BookedLines(appointment.StaffMemberId, lines));
    }
}
