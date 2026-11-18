using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Authorization;

public class UserRole : SoftDeletableEntity, IHasOptionalOwner<Guid?>
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid? OwnerId { get; set; }
    public Company? Owner { get; set; }
}
