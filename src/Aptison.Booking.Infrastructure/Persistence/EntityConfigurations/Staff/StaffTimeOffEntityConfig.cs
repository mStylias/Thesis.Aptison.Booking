using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Staff;

public class StaffTimeOffEntityConfig : IEntityTypeConfiguration<StaffTimeOff>
{
    public void Configure(EntityTypeBuilder<StaffTimeOff> builder)
    {
        builder.ConfigureEntity("staff_time_off");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(timeOff => timeOff.StartsAt).HasColumnType("timestamp with time zone");
        builder.Property(timeOff => timeOff.EndsAt).HasColumnType("timestamp with time zone");
        builder.Property(timeOff => timeOff.Reason).HasColumnType("text");

        builder.HasOne(timeOff => timeOff.Owner).WithMany().HasForeignKey(timeOff => timeOff.OwnerId).OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(timeOff => timeOff.StaffMember)
            .WithMany()
            .HasForeignKey(timeOff => new { timeOff.StaffMemberId, timeOff.OwnerId })
            .HasPrincipalKey(member => new { member.Id, member.OwnerId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(timeOff => new { timeOff.StaffMemberId, timeOff.StartsAt, timeOff.EndsAt })
            .HasDatabaseName("ix_staff_time_off_staff_range")
            .HasFilter("NOT is_deleted");
    }
}
