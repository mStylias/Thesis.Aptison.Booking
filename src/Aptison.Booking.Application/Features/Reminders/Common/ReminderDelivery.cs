using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Domain.Entities.Appointments;
using Microsoft.Extensions.Logging;

namespace Aptison.Booking.Application.Features.Reminders.Common;

internal static class ReminderDelivery
{
    /// <summary>The warning is what the 15-minute lateness alert keys on, beside the <c>reminders.delay</c> histogram.</summary>
    public static void RecordSent(
        string channel,
        string kind,
        Guid appointmentId,
        DateTime dueAt,
        DateTime now,
        AppointmentReminderOptions options,
        ILogger logger)
    {
        var delay = now - dueAt;

        ReminderMetrics.RecordOutcome(channel, kind, AppointmentReminderStatus.Sent);
        ReminderMetrics.RecordDelay(channel, kind, delay);

        if (delay > options.LateWarningThreshold)
        {
            logger.LogWarning(
                "Sent the {Kind} {Channel} reminder for appointment {AppointmentId} {DelayMinutes:0} minutes late",
                kind, channel, appointmentId, delay.TotalMinutes);
        }
        else
        {
            logger.LogInformation(
                "Sent the {Kind} {Channel} reminder for appointment {AppointmentId}", kind, channel, appointmentId);
        }
    }
}
