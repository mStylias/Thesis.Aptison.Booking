namespace Aptison.Booking.Domain.Entities.Appointments;

/// <summary>The reminders every active appointment gets. No <c>CHECK</c> constraint, so never spell one as a literal.</summary>
public static class AppointmentReminderKind
{
    public const string DayBefore = "day-before";
    public const string TwoHours = "two-hours";

    public static readonly TimeSpan DayBeforeOffset = TimeSpan.FromHours(24);
    public static readonly TimeSpan TwoHoursOffset = TimeSpan.FromHours(2);
}
