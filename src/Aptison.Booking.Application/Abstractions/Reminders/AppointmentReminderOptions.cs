namespace Aptison.Booking.Application.Abstractions.Reminders;

public sealed class AppointmentReminderOptions
{
    public const string SectionName = "Reminders";

    /// <summary>
    /// Signs the unsubscribe links, at least 32 characters. A secret: changing it breaks every
    /// link in every reminder already sent.
    /// </summary>
    public string UnsubscribeSecret { get; set; } = string.Empty;

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan RetentionSweepInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Records are deleted this long after the appointment's start.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>Registering one more removes the device not seen for the longest time.</summary>
    public int MaxDevicesPerUser { get; set; } = 20;

    /// <summary>A reminder sent later than this after its due time logs a warning.</summary>
    public TimeSpan LateWarningThreshold { get; set; } = TimeSpan.FromMinutes(15);
}
