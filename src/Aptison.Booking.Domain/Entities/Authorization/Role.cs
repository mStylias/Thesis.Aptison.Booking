using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Authorization;

public class Role : SoftDeletableEntity, IHasOptionalOwner<Guid?>
{
    public Guid? OwnerId { get; set; }
    public Company? Owner { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    /// <summary>
    /// Hidden from every caller without <c>platform:manage</c>, and not grantable by one. Reads
    /// only — see <c>RoleVisibility</c>; what the role grants to those holding it is unchanged.
    /// </summary>
    public bool IsPlatformOnly { get; set; }

    // Counted, never projected through: a Count is a correlated subquery with the filters applied
    // inside it, so a filtered-out grant is an absence rather than a LEFT JOIN's null.
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
