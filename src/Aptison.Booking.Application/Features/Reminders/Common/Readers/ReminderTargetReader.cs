using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Reminders.Common.Readers;

internal static class ReminderTargetReader
{
    /// <summary>
    /// Null when the appointment is gone. Callers read under <c>BecomeAllOwners</c>: an outbox
    /// handler has no company, and the appointment and a walk-in customer are both owned.
    /// </summary>
    public static async Task<ReminderTarget?> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await unitOfWork.GetRepository<Appointment>()
            .QuerySelect(
                row => new { row.Status, row.StartsAt, row.CustomerId },
                query => query.Where(row => row.Id == appointmentId))
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return null;
        }

        var customer = await unitOfWork.GetRepository<User>()
            .QuerySelect(
                user => new { user.Email, user.EmailRemindersEnabled },
                query => query.Where(user => user.Id == appointment.CustomerId))
            .FirstOrDefaultAsync(cancellationToken);

        return new ReminderTarget(
            appointment.Status,
            appointment.StartsAt,
            appointment.CustomerId,
            customer?.Email,
            customer?.EmailRemindersEnabled ?? false);
    }
}
