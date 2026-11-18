using Aptison.Booking.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Authorization;

public class RolePermissionEntityConfig : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });

        builder.HasOne(rolePermission => rolePermission.Role).WithMany().HasForeignKey(rolePermission => rolePermission.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(rolePermission => rolePermission.Permission).WithMany().HasForeignKey(rolePermission => rolePermission.PermissionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rolePermission => rolePermission.PermissionId)
            .HasDatabaseName("ix_role_permissions_permission");
    }
}
