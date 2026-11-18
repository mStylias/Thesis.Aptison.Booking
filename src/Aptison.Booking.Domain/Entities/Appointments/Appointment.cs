using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Appointments;

public class Appointment : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public Guid StaffMemberId { get; set; }
    public StaffMember StaffMember { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public User Customer { get; set; } = null!;

    // What the customer sees.
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    // What the calendar blocks: the above widened by the services' buffers. Not generated columns —
    // the buffers come through appointment_services. appointments_no_double_booking keys on this pair.
    public DateTime BlockedFrom { get; set; }
    public DateTime BlockedUntil { get; set; }

    // starts_at and ends_at in the shop's timezone, written by the appointments_local_times trigger.
    public DateOnly LocalDate { get; set; }
    public TimeOnly LocalStartTime { get; set; }
    public TimeOnly LocalEndTime { get; set; }

    public string Status { get; set; } = AppointmentStatus.Pending;
    public string Source { get; set; } = AppointmentSource.Online;

    // Staff set the end; EndsAt is then no longer the sum of the lines' durations.
    public bool DurationOverridden { get; set; }

    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary><see cref="CancellationReason"/> folded by the database; the grid's reason filter targets it.</summary>
    public string? CancellationReasonNormalized { get; private set; }

    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledById { get; set; }
    public User? CancelledBy { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// The calendar invitation's SEQUENCE. Raise it on every customer-visible change and on cancel;
    /// the appointments_calendar_sequence trigger stores OLD + 1, so two concurrent raises never share a value.
    /// </summary>
    public int CalendarSequence { get; set; }

    /// <summary>
    /// When it was booked or its start last moved, written by the appointments_scheduled_at trigger.
    /// A reminder due less than an hour after this is skipped.
    /// </summary>
    public DateTime ScheduledAt { get; set; }

    public ICollection<AppointmentService> Services { get; set; } = [];

    public ICollection<AppointmentReminder> Reminders { get; set; } = [];

    /// <summary>The live review, if any. A soft-deleted one is filtered out, so the appointment reads as not rated.</summary>
    public Review? Review { get; set; }
}
