using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Companies;

public class CompanyBusinessTypeEntityConfig : IEntityTypeConfiguration<CompanyBusinessType>
{
    public void Configure(EntityTypeBuilder<CompanyBusinessType> builder)
    {
        builder.ToTable("company_business_types");
        builder.HasKey(companyBusinessType => new { companyBusinessType.CompanyId, companyBusinessType.BusinessTypeId });

        builder.Property(companyBusinessType => companyBusinessType.IsPrimary).HasDefaultValue(false);

        builder.HasOne(companyBusinessType => companyBusinessType.Company).WithMany(company => company.BusinessTypes).HasForeignKey(companyBusinessType => companyBusinessType.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(companyBusinessType => companyBusinessType.BusinessType).WithMany().HasForeignKey(companyBusinessType => companyBusinessType.BusinessTypeId).OnDelete(DeleteBehavior.NoAction);

        // Exactly one primary type per company. A partial unique index, not a CHECK — this is a
        // uniqueness rule, and it is also the index the browse query uses.
        builder.HasIndex(companyBusinessType => companyBusinessType.CompanyId)
            .IsUnique()
            .HasDatabaseName("ux_company_business_types_primary")
            .HasFilter("is_primary");

        // "All barbershops" browse query.
        builder.HasIndex(companyBusinessType => companyBusinessType.BusinessTypeId)
            .HasDatabaseName("ix_company_business_types_type");
    }
}
