using Aptison.Booking.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.System;

public class AuditLogEntityConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(log => new { log.Id, log.OccurredAt });

        builder.Property(log => log.OccurredAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.Property(log => log.Action).HasMaxLength(64);
        builder.Property(log => log.EntityType).HasMaxLength(64);
        builder.Property(log => log.IpAddress).HasColumnType("inet");
        builder.Property(log => log.UserAgent).HasColumnType("text");
        builder.Property(log => log.Data).HasColumnType("jsonb");

        builder.HasOne(log => log.Actor).WithMany().HasForeignKey(log => log.ActorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(log => log.Owner).WithMany().HasForeignKey(log => log.OwnerId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(log => log.OccurredAt)
            .HasMethod("brin")
            .HasDatabaseName("ix_audit_logs_occurred_at_brin");

        builder.HasIndex(log => new { log.ActorId, log.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_audit_logs_actor");

        builder.HasIndex(log => new { log.EntityType, log.EntityId, log.OccurredAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_audit_logs_entity");

        builder.HasIndex(log => new { log.OwnerId, log.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_audit_logs_owner");

        builder.HasIndex(log => log.Data)
            .HasMethod("gin")
            .HasDatabaseName("ix_audit_logs_data_gin");
    }
}
