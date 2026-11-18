using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Companies;

public class CompanyHolidayEntityConfig : IEntityTypeConfiguration<CompanyHoliday>
{
    public void Configure(EntityTypeBuilder<CompanyHoliday> builder)
    {
        builder.ConfigureEntity("company_holidays");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(holiday => holiday.StartsOn).HasColumnType("date");
        builder.Property(holiday => holiday.EndsOn).HasColumnType("date");
        builder.Property(holiday => holiday.IsClosed).HasDefaultValue(true);
        builder.Property(holiday => holiday.OverrideOpenTime).HasColumnType("time without time zone");
        builder.Property(holiday => holiday.OverrideCloseTime).HasColumnType("time without time zone");
        builder.Property(holiday => holiday.Name).HasMaxLength(200);

        builder.HasOne(holiday => holiday.Owner).WithMany().HasForeignKey(holiday => holiday.OwnerId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(holiday => new { holiday.OwnerId, holiday.StartsOn, holiday.EndsOn })
            .HasDatabaseName("ix_company_holidays_owner_range")
            .HasFilter("NOT is_deleted");
    }
}
