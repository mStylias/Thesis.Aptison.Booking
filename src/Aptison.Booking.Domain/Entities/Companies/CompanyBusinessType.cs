using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Companies;

public class CompanyBusinessType : IEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid BusinessTypeId { get; set; }
    public BusinessType BusinessType { get; set; } = null!;

    // Exactly one per company, guarded by ux_company_business_types_primary.
    public bool IsPrimary { get; set; }
}
