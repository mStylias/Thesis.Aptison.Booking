using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Appointments;

public class AppointmentService : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public decimal PriceAtBooking { get; set; }
    public int DurationAtBookingMinutes { get; set; }
}
