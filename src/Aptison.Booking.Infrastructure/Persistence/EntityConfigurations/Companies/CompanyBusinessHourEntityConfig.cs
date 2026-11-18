using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Companies;

public class CompanyBusinessHourEntityConfig : IEntityTypeConfiguration<CompanyBusinessHour>
{
    public void Configure(EntityTypeBuilder<CompanyBusinessHour> builder)
    {
        builder.ConfigureEntity("company_business_hours");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(hour => hour.OpenTime).HasColumnType("time without time zone");
        builder.Property(hour => hour.CloseTime).HasColumnType("time without time zone");

        builder.HasOne(hour => hour.Owner).WithMany(company => company.BusinessHours).HasForeignKey(hour => hour.OwnerId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(hour => new { hour.OwnerId, hour.DayOfWeek })
            .HasDatabaseName("ix_company_business_hours_owner_day")
            .HasFilter("NOT is_deleted");
    }
}
