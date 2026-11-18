using Aptison.Booking.Domain.Entities.Authorization;

namespace Aptison.Booking.Tests.Common.Data;

/// <summary>
/// Roles and grants. A null <c>companyId</c> is a platform-scope role or grant, which is a
/// different thing from a company-scope one and not a weaker version of it.
/// </summary>
public static class RoleFactory
{
    public static Role Create(
        Guid? id = null,
        string? name = null,
        Guid? companyId = null,
        bool isSystem = false,
        bool isPlatformOnly = false,
        string? description = null)
    {
        var roleId = id ?? Guid.CreateVersion7();

        return new Role
        {
            Id = roleId,
            OwnerId = companyId,
            Name = name ?? $"role-{roleId:N}",
            Description = description,
            IsSystem = isSystem,
            IsPlatformOnly = isPlatformOnly
        };
    }

    public static UserRole CreateGrant(Guid userId, Guid roleId, Guid? companyId = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            RoleId = roleId,
            OwnerId = companyId
        };

    public static RolePermission CreatePermissionLink(Guid roleId, Guid permissionId) =>
        new()
        {
            RoleId = roleId,
            PermissionId = permissionId
        };
}
