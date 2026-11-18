using Aptison.Booking.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Authorization;

public class UserRoleEntityConfig : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ConfigureEntity("user_roles");
        builder.ConfigureSoftDeletableEntity();

        builder.HasOne(userRole => userRole.User).WithMany().HasForeignKey(userRole => userRole.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(userRole => userRole.Role).WithMany(role => role.UserRoles).HasForeignKey(userRole => userRole.RoleId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(userRole => userRole.Owner).WithMany().HasForeignKey(userRole => userRole.OwnerId).OnDelete(DeleteBehavior.NoAction);

        // One active (user, role) assignment per company scope. NULLs are distinct
        // in Postgres, so global-scope assignments are guarded separately below.
        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId, userRole.OwnerId })
            .IsUnique()
            .HasDatabaseName("ux_user_roles_user_role_owner")
            .HasFilter("NOT is_deleted AND owner_id IS NOT NULL");

        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId })
            .IsUnique()
            .HasDatabaseName("ux_user_roles_user_role_global")
            .HasFilter("NOT is_deleted AND owner_id IS NULL");

        builder.HasIndex(userRole => userRole.UserId)
            .HasDatabaseName("ix_user_roles_user")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(userRole => new { userRole.OwnerId, userRole.RoleId })
            .HasDatabaseName("ix_user_roles_owner_role")
            .HasFilter("NOT is_deleted AND owner_id IS NOT NULL");
    }
}
