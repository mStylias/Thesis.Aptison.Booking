using Aptison.Booking.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Authorization;

public class PermissionEntityConfig : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ConfigureEntity("permissions");

        builder.Property(permission => permission.Code).HasMaxLength(128);
        builder.Property(permission => permission.Name).HasMaxLength(128);
        builder.Property(permission => permission.Description).HasColumnType("text");
        builder.Property(permission => permission.Category).HasMaxLength(64);
        builder.Property(permission => permission.IsTenantGrantable).HasDefaultValue(true);

        builder.HasIndex(permission => permission.Code)
            .IsUnique()
            .HasDatabaseName("ux_permissions_code");
    }
}
