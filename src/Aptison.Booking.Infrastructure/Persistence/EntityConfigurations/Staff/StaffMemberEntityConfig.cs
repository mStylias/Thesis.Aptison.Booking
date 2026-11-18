using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Staff;

public class StaffMemberEntityConfig : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.ConfigureEntity("staff_members");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(member => member.JobTitleCode).HasMaxLength(64);
        builder.Property(member => member.AvatarStorageKey).HasMaxLength(500);
        builder.Property(member => member.IsBookable).HasDefaultValue(true);
        builder.Property(member => member.IsActive).HasDefaultValue(true);
        builder.Property(member => member.JoinedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        // Principal key for the composite tenant-consistency FKs on staff_work_hours,
        // staff_time_off, staff_services, appointments and reviews. It makes filing a child row
        // under company A for a staff member employed at company B structurally impossible.
        builder.HasAlternateKey(member => new { member.Id, member.OwnerId })
            .HasName("ux_staff_members_id_owner");

        builder.HasOne(member => member.Owner).WithMany().HasForeignKey(member => member.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(member => member.User).WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(member => member.UserId)
            .IsUnique()
            .HasDatabaseName("ux_staff_members_user")
            .HasFilter("is_active AND NOT is_deleted");

        // Both index owner_id, so they need distinct model-level names — the single-argument
        // HasIndex overload would return the same index twice.
        builder.HasIndex(member => member.OwnerId, "ix_staff_members_owner_active")
            .HasDatabaseName("ix_staff_members_owner_active")
            .HasFilter("is_active AND NOT is_deleted");

        // Booking UI: who can actually take appointments at this company.
        builder.HasIndex(member => member.OwnerId, "ix_staff_members_owner_bookable")
            .HasDatabaseName("ix_staff_members_owner_bookable")
            .HasFilter("is_bookable AND is_active AND NOT is_deleted");
    }
}
