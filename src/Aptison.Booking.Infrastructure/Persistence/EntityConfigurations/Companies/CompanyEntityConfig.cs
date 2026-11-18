using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Companies;

public class CompanyEntityConfig : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ConfigureEntity("companies");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(company => company.Slug).HasMaxLength(80);
        builder.Property(company => company.Email).HasColumnType("citext");
        builder.Property(company => company.Phone).HasMaxLength(32);
        builder.Property(company => company.Website).HasMaxLength(500);
        builder.Property(company => company.LogoStorageKey).HasColumnName("logo_url").HasMaxLength(500);
        builder.Property(company => company.Location).HasColumnType("geography (point, 4326)");
        builder.Property(company => company.PostalCode).HasMaxLength(20);
        builder.Property(company => company.CountryCode).HasColumnType("char(2)");
        builder.Property(company => company.Timezone).HasMaxLength(50);
        builder.Property(company => company.Currency).HasColumnType("char(3)").HasDefaultValue("EUR");
        builder.Property(company => company.SlotIntervalMinutes).HasDefaultValue(15);
        builder.Property(company => company.MinLeadTimeMinutes).HasDefaultValue(0);
        builder.Property(company => company.MaxAdvanceDays).HasDefaultValue(60);
        builder.Property(company => company.CancellationCutoffMinutes).HasDefaultValue(0);
        builder.Property(company => company.IsActive).HasDefaultValue(true);
        builder.ConfigureAttributes(company => company.Attributes);

        // Unfiltered: a deleted shop keeps its slug, so an old link never lands on another shop.
        builder.HasIndex(company => company.Slug)
            .IsUnique()
            .HasDatabaseName("ux_companies_slug");

        builder.HasIndex(company => company.Location)
            .HasMethod("gist")
            .HasDatabaseName("ix_companies_location")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(company => company.CountryCode)
            .HasDatabaseName("ix_companies_country_code")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(company => company.Attributes)
            .HasMethod("gin")
            .HasDatabaseName("ix_companies_attributes");
    }
}
