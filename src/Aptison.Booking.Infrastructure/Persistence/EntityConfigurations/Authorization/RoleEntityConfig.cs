using Aptison.Booking.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Authorization;

public class RoleEntityConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ConfigureEntity("roles");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(role => role.Name).HasMaxLength(64);
        builder.Property(role => role.Description).HasColumnType("text");
        builder.Property(role => role.IsSystem).HasDefaultValue(false);
        builder.Property(role => role.IsPlatformOnly).HasDefaultValue(false);

        builder.HasOne(role => role.Owner).WithMany().HasForeignKey(role => role.OwnerId).OnDelete(DeleteBehavior.NoAction);

        // Unique role name per company. NULLs are distinct in Postgres, so global
        // (system) roles are guarded by a separate index below.
        builder.HasIndex(role => new { role.OwnerId, role.Name })
            .IsUnique()
            .HasDatabaseName("ux_roles_owner_name")
            .HasFilter("NOT is_deleted AND owner_id IS NOT NULL");

        builder.HasIndex(role => role.Name)
            .IsUnique()
            .HasDatabaseName("ux_roles_global_name")
            .HasFilter("NOT is_deleted AND owner_id IS NULL");

        builder.HasIndex(role => role.OwnerId)
            .HasDatabaseName("ix_roles_owner")
            .HasFilter("NOT is_deleted AND owner_id IS NOT NULL");
    }
}
