using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Services;

public class StaffService : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public Guid StaffMemberId { get; set; }
    public StaffMember StaffMember { get; set; } = null!;
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public decimal? CustomPrice { get; set; }
    public int? CustomDurationMinutes { get; set; }
}
