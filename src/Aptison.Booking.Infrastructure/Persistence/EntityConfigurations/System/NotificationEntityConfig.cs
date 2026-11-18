using Aptison.Booking.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.System;

public class NotificationEntityConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ConfigureEntity("notifications");

        builder.Property(notification => notification.Type).HasMaxLength(64);
        builder.Property(notification => notification.Title).HasMaxLength(200);
        builder.Property(notification => notification.Message).HasColumnType("text");
        builder.Property(notification => notification.Data).HasColumnType("jsonb");
        builder.Property(notification => notification.ReadAt).HasColumnType("timestamp with time zone");
        builder.Property(notification => notification.CreatedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        builder.HasOne(notification => notification.User).WithMany().HasForeignKey(notification => notification.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt }, "ix_notifications_user_created")
            .IsDescending(false, true)
            .HasDatabaseName("ix_notifications_user_created");

        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt }, "ix_notifications_user_unread")
            .IsDescending(false, true)
            .HasDatabaseName("ix_notifications_user_unread")
            .HasFilter("read_at IS NULL");
    }
}
