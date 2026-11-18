using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Patching;

namespace Aptison.Booking.Application.Features.Reminders.Common;

/// <summary>
/// Outcome writes on a reminder record. A push row is shared by every device of the customer, so
/// nothing but a delivery overwrites <see cref="AppointmentReminderStatus.Sent"/>.
/// </summary>
internal static class AppointmentReminderRecords
{
    private const int MaxErrorLength = 2_000;

    public static Task<string?> ReadStatusAsync(
        IRepository<AppointmentReminder, BookingContext> reminderRepo, Guid reminderId, CancellationToken cancellationToken) =>
        reminderRepo
            .QuerySelect(reminder => reminder.Status, query => query.Where(reminder => reminder.Id == reminderId))
            .FirstOrDefaultAsync(cancellationToken);

    public static Task MarkSentAsync(
        IRepository<AppointmentReminder, BookingContext> reminderRepo, Guid reminderId, DateTime now, CancellationToken cancellationToken) =>
        reminderRepo.ExecutePatchAsync(
            query => query.Where(reminder => reminder.Id == reminderId),
            new AppointmentReminderOutcomePatch
            {
                Status = PatchValue<string>.Of(AppointmentReminderStatus.Sent),
                Reason = PatchValue<string?>.Of(null),
                Error = PatchValue<string?>.Of(null),
                CompletedAt = PatchValue<DateTime?>.Of(now)
            },
            cancellationToken);

    public static Task MarkDroppedAsync(
        IRepository<AppointmentReminder, BookingContext> reminderRepo,
        Guid reminderId,
        string reason,
        DateTime now,
        CancellationToken cancellationToken) =>
        reminderRepo.ExecutePatchAsync(
            query => query.Where(reminder => reminder.Id == reminderId && reminder.Status != AppointmentReminderStatus.Sent),
            new AppointmentReminderOutcomePatch
            {
                Status = PatchValue<string>.Of(AppointmentReminderStatus.Dropped),
                Reason = PatchValue<string?>.Of(reason),
                CompletedAt = PatchValue<DateTime?>.Of(now)
            },
            cancellationToken);

    public static Task MarkFailedAsync(
        IRepository<AppointmentReminder, BookingContext> reminderRepo,
        Guid reminderId,
        Exception exception,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var error = $"{exception.GetType().Name}: {exception.Message}";

        return reminderRepo.ExecutePatchAsync(
            query => query.Where(reminder => reminder.Id == reminderId && reminder.Status != AppointmentReminderStatus.Sent),
            new AppointmentReminderOutcomePatch
            {
                Status = PatchValue<string>.Of(AppointmentReminderStatus.Failed),
                Error = PatchValue<string?>.Of(error.Length > MaxErrorLength ? error[..MaxErrorLength] : error),
                CompletedAt = PatchValue<DateTime?>.Of(now)
            },
            cancellationToken);
    }
}
