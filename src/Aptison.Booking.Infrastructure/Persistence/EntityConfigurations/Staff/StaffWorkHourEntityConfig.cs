using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Staff;

public class StaffWorkHourEntityConfig : IEntityTypeConfiguration<StaffWorkHour>
{
    public void Configure(EntityTypeBuilder<StaffWorkHour> builder)
    {
        builder.ConfigureEntity("staff_work_hours");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(hour => hour.StartTime).HasColumnType("time without time zone");
        builder.Property(hour => hour.EndTime).HasColumnType("time without time zone");

        builder.HasOne(hour => hour.Owner).WithMany().HasForeignKey(hour => hour.OwnerId).OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(hour => hour.StaffMember)
            .WithMany(member => member.WorkHours)
            .HasForeignKey(hour => new { hour.StaffMemberId, hour.OwnerId })
            .HasPrincipalKey(member => new { member.Id, member.OwnerId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(hour => new { hour.StaffMemberId, hour.DayOfWeek })
            .HasDatabaseName("ix_staff_work_hours_staff_day")
            .HasFilter("NOT is_deleted");
    }
}
