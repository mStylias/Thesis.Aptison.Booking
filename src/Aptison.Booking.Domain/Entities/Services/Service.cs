using System.Text.Json;
using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Services;

public class Service : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;

    // Nullable: uncategorised services stay valid.
    public Guid? CategoryId { get; set; }
    public ServiceCategory? Category { get; set; }

    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }

    // Turnaround either side of the appointment. Blocks the calendar without being visible
    // to the customer; folded into appointments.blocked_from/blocked_until at booking time.
    public int BufferBeforeMinutes { get; set; }
    public int BufferAfterMinutes { get; set; }

    // Vertical-specific fields that do not earn a column of their own.
    public Dictionary<string, JsonElement> Attributes { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public ICollection<ServiceTranslation> Translations { get; set; } = [];
    public ICollection<StaffService> StaffServices { get; set; } = [];
    public ICollection<AppointmentService> AppointmentServices { get; set; } = [];
}
