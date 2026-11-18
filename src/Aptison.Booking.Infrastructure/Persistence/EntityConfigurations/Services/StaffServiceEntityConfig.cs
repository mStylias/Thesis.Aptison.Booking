using Aptison.Booking.Domain.Entities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Services;

public class StaffServiceEntityConfig : IEntityTypeConfiguration<StaffService>
{
    public void Configure(EntityTypeBuilder<StaffService> builder)
    {
        builder.ConfigureEntity("staff_services");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(staffService => staffService.CustomPrice).HasPrecision(10, 2);

        builder.HasOne(staffService => staffService.Owner).WithMany().HasForeignKey(staffService => staffService.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(staffService => staffService.Service).WithMany(service => service.StaffServices).HasForeignKey(staffService => staffService.ServiceId).OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(staffService => staffService.StaffMember)
            .WithMany()
            .HasForeignKey(staffService => new { staffService.StaffMemberId, staffService.OwnerId })
            .HasPrincipalKey(member => new { member.Id, member.OwnerId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(staffService => new { staffService.StaffMemberId, staffService.ServiceId })
            .IsUnique()
            .HasDatabaseName("ux_staff_services_staff_service")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(staffService => staffService.ServiceId)
            .HasDatabaseName("ix_staff_services_service")
            .HasFilter("NOT is_deleted");
    }
}
