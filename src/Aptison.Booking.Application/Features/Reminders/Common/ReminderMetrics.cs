using System.Diagnostics.Metrics;
using Aptison.Booking.Application.Common.Observability;

namespace Aptison.Booking.Application.Features.Reminders.Common;

/// <summary>Per channel and kind: sent, skipped, dropped and failed with the rule that decided it, dead devices, and lateness.</summary>
internal static class ReminderMetrics
{
    private static readonly Meter Meter = new(Telemetry.RemindersMeterName);

    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "reminders.outcomes", "{reminder}", "Reminder outcomes per channel, kind, outcome and reason.");

    private static readonly Counter<long> DeadDevices = Meter.CreateCounter<long>(
        "reminders.push.dead_devices", "{device}", "Push devices removed because the push service no longer knows them.");

    private static readonly Histogram<double> Delay = Meter.CreateHistogram<double>(
        "reminders.delay", "s", "Time from a reminder's due time to its delivery.");

    public static void RecordOutcome(string channel, string kind, string outcome, string? reason = null) =>
        Outcomes.Add(
            1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("reason", reason ?? "none"));

    public static void RecordDeadDevice() => DeadDevices.Add(1);

    public static void RecordDelay(string channel, string kind, TimeSpan delay) =>
        Delay.Record(
            Math.Max(0, delay.TotalSeconds),
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("kind", kind));
}
