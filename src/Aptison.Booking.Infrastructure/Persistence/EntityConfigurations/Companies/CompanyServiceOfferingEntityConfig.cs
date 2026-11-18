using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Companies;

public class CompanyServiceOfferingEntityConfig : IEntityTypeConfiguration<CompanyServiceOffering>
{
    public void Configure(EntityTypeBuilder<CompanyServiceOffering> builder)
    {
        builder.ToTable("company_service_offerings");
        builder.HasKey(offering => new { offering.CompanyId, offering.StaffMemberId, offering.ServiceId });

        builder.Property(offering => offering.Price).HasPrecision(10, 2);

        builder.HasOne(offering => offering.Company).WithMany(company => company.ServiceOfferings).HasForeignKey(offering => offering.CompanyId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(offering => offering.ServiceId)
            .HasDatabaseName("ix_company_service_offerings_service");

        builder.HasIndex(offering => offering.CategoryId)
            .HasDatabaseName("ix_company_service_offerings_category");
    }
}
