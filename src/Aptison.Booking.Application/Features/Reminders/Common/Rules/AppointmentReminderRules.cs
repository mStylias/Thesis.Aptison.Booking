using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Reminders.Common.Rules;

/// <summary>When each reminder is due, and which ones a scan or a send must leave out (plan §3.2).</summary>
public static class AppointmentReminderRules
{
    /// <summary>The booked or changed email has just arrived, so a reminder this soon after it is noise.</summary>
    public static readonly TimeSpan QuietAfterScheduling = TimeSpan.FromHours(1);

    public static DateTime GetDueAt(string kind, DateTime startsAt) =>
        startsAt - (kind == AppointmentReminderKind.TwoHours
            ? AppointmentReminderKind.TwoHoursOffset
            : AppointmentReminderKind.DayBeforeOffset);

    /// <summary>
    /// The reminders to claim at <paramref name="now"/>. Once <c>two-hours</c> is due an unclaimed
    /// <c>day-before</c> is claimed as skipped, so it can never arrive after it.
    /// </summary>
    public static IReadOnlyList<ReminderDecision> Decide(
        DateTime startsAt, DateTime scheduledAt, bool dayBeforeClaimed, bool twoHoursClaimed, DateTime now)
    {
        if (now >= startsAt || twoHoursClaimed)
        {
            return [];
        }

        var dayBeforeDueAt = GetDueAt(AppointmentReminderKind.DayBefore, startsAt);
        var twoHoursDueAt = GetDueAt(AppointmentReminderKind.TwoHours, startsAt);

        if (now >= twoHoursDueAt)
        {
            var twoHours = new ReminderDecision(
                AppointmentReminderKind.TwoHours, twoHoursDueAt, GetQuietReason(twoHoursDueAt, scheduledAt));

            if (dayBeforeClaimed)
            {
                return [twoHours];
            }

            var dayBefore = new ReminderDecision(
                AppointmentReminderKind.DayBefore,
                dayBeforeDueAt,
                GetQuietReason(dayBeforeDueAt, scheduledAt) ?? AppointmentReminderReason.Superseded);

            return [dayBefore, twoHours];
        }

        if (now >= dayBeforeDueAt && !dayBeforeClaimed)
        {
            return [new ReminderDecision(
                AppointmentReminderKind.DayBefore, dayBeforeDueAt, GetQuietReason(dayBeforeDueAt, scheduledAt))];
        }

        return [];
    }

    /// <summary>
    /// Why a claimed reminder must not go out after all, checked again when the message is built:
    /// the appointment is the one in the database now, not the one the scan saw.
    /// </summary>
    public static string? GetDropReason(
        string? status, DateTime? currentStartsAt, string kind, DateTime claimedStartsAt, DateTime now)
    {
        if (status is null || !AppointmentStatusRules.IsActive(status))
        {
            return AppointmentReminderReason.AppointmentInactive;
        }

        if (currentStartsAt != claimedStartsAt)
        {
            return AppointmentReminderReason.TimeChanged;
        }

        if (now >= claimedStartsAt)
        {
            return AppointmentReminderReason.Started;
        }

        // A day-before retried past the two-hours due time would arrive after the two-hours one.
        return kind == AppointmentReminderKind.DayBefore
               && now >= GetDueAt(AppointmentReminderKind.TwoHours, claimedStartsAt)
            ? AppointmentReminderReason.Superseded
            : null;
    }

    private static string? GetQuietReason(DateTime dueAt, DateTime scheduledAt) =>
        dueAt < scheduledAt + QuietAfterScheduling ? AppointmentReminderReason.TooSoonAfterBooking : null;
}
