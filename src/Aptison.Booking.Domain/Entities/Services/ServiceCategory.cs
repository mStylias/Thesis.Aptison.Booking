using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Services;

public class ServiceCategory : SoftDeletableEntity, IHasOptionalOwner<Guid?>
{
    // Platform catalog for a vertical (owner_id null), or a company-specific category
    // (business_type_id null). At least one of the two must be set — enforced in the
    // application layer, not by a CHECK constraint.
    public Guid? BusinessTypeId { get; set; }
    public BusinessType? BusinessType { get; set; }
    public Guid? OwnerId { get; set; }
    public Company? Owner { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceCategoryTranslation> Translations { get; set; } = [];
    public ICollection<Service> Services { get; set; } = [];
}
