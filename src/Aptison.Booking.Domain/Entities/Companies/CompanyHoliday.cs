using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Companies;

public class CompanyHoliday : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsClosed { get; set; } = true;
    public TimeOnly? OverrideOpenTime { get; set; }
    public TimeOnly? OverrideCloseTime { get; set; }
    public string? Name { get; set; }
}
