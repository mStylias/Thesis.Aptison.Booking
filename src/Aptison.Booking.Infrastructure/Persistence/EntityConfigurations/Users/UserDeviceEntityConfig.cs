using Aptison.Booking.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Users;

public class UserDeviceEntityConfig : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ConfigureEntity("user_devices");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(device => device.Platform).HasMaxLength(16);
        builder.Property(device => device.PushData).HasColumnType("jsonb");
        builder.Property(device => device.DeviceName).HasMaxLength(200);
        builder.Property(device => device.AppVersion).HasMaxLength(50);
        builder.Property(device => device.LastSeenAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.Property(device => device.IsActive).HasDefaultValue(true);

        builder.HasOne(device => device.User).WithMany().HasForeignKey(device => device.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(device => device.UserId)
            .HasDatabaseName("ix_user_devices_user_active")
            .HasFilter("is_active AND NOT is_deleted");
    }
}
