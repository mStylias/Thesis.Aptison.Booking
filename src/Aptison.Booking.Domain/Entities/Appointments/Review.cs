using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Appointments;

public class Review : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public User Customer { get; set; } = null!;
    public Guid StaffMemberId { get; set; }
    public StaffMember StaffMember { get; set; } = null!;
    public short Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsPublished { get; set; } = true;
}
