using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Users;

public class UserBan : AuditableEntity, IHasOptionalOwner<Guid?>
{
    public Guid? OwnerId { get; set; }
    public Company? Owner { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BannedById { get; set; }
    public User BannedBy { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime BannedAt { get; set; }
    public DateTime? LiftedAt { get; set; }
    public Guid? LiftedById { get; set; }
    public User? LiftedBy { get; set; }
}
