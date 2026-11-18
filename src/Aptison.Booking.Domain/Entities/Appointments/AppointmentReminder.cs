namespace Aptison.Booking.Domain.Entities.Appointments;

/// <summary>
/// One reminder kind on one channel for one start time of an appointment. The row is the claim:
/// <c>ux_appointment_reminders</c> is what makes a reminder go out once across instances and retries.
/// </summary>
public class AppointmentReminder : Entity
{
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    /// <summary>One of <see cref="AppointmentReminderKind"/>.</summary>
    public string Kind { get; set; } = null!;

    /// <summary>The start time this reminder was for. A new time is a new pair of reminders.</summary>
    public DateTime StartsAt { get; set; }

    /// <summary>One of <see cref="AppointmentReminderChannel"/>.</summary>
    public string Channel { get; set; } = null!;

    /// <summary>One of <see cref="AppointmentReminderStatus"/>.</summary>
    public string Status { get; set; } = null!;

    /// <summary>Why it was skipped or dropped. One of <see cref="AppointmentReminderReason"/>.</summary>
    public string? Reason { get; set; }

    public string? Error { get; set; }

    public DateTime DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
