using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Synax.Identity;

namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// The rebuild path behind the cache. Every read lifts company isolation via
/// <see cref="IUserContextScope.BecomeAllOwners"/>, because what it feeds is unscoped: the role map
/// is global and a user's access spans every company. Building either under a company scope caches a
/// partial answer under a key that claims to be complete. Scope is applied afterwards, in memory,
/// by <see cref="PermissionResolver"/>.
/// </summary>
internal sealed class PermissionStore(BookingDbContext dbContext, IUserContextScope userContext)
    : IPermissionStore
{
    public async Task<UserAccessEntry> ReadUserAccessAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        using var _ = userContext.BecomeAllOwners();

        // Joined to companies so a soft-deleted company gives no scope. ux_staff_members_user
        // allows one active row, so there is at most one match; inactive rows elsewhere are ignored.
        var staffCompanyId = await (
                from member in dbContext.Set<StaffMember>().AsNoTracking()
                join company in dbContext.Set<Company>().AsNoTracking()
                    on member.OwnerId equals company.Id
                where member.UserId == userId && member.IsActive
                select (Guid?)company.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Explicit join, not ur.Role: soft-delete filters reach a navigation as a LEFT JOIN, making
        // `role.OwnerId == null` true for a filtered-out role and granting it everywhere.
        var grants =
            from assignment in dbContext.Set<UserRole>().AsNoTracking()
            join role in dbContext.Set<Role>().AsNoTracking()
                on assignment.RoleId equals role.Id
            where assignment.UserId == userId
                  // Read-side backstop for "a company-owned role is grantable only at its own
                  // company" — any path writing user_roles outside AssignRole bypasses that.
                  && (role.OwnerId == null || role.OwnerId == assignment.OwnerId)
            select new RoleGrant(assignment.RoleId, assignment.OwnerId);

        return new UserAccessEntry(staffCompanyId, await grants.ToListAsync(cancellationToken));
    }

    public async Task<IReadOnlyDictionary<Guid, string[]>> ReadRolePermissionsAsync(
        CancellationToken cancellationToken)
    {
        using var _ = userContext.BecomeAllOwners();

        // Two queries: a role granting no permissions must still appear in the result.
        var roleIds = await dbContext.Set<Role>()
            .AsNoTracking()
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        var rolePermissionRows = await (
                from rolePermission in dbContext.Set<RolePermission>().AsNoTracking()
                join role in dbContext.Set<Role>().AsNoTracking()
                    on rolePermission.RoleId equals role.Id
                select new { rolePermission.RoleId, rolePermission.Permission.Code })
            .ToListAsync(cancellationToken);

        var codesByRole = rolePermissionRows
            .GroupBy(pair => pair.RoleId)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Code).ToArray());

        return roleIds.ToDictionary(
            roleId => roleId,
            roleId => codesByRole.TryGetValue(roleId, out var permissionCodes) ? permissionCodes : []);
    }

    public async Task<IReadOnlySet<string>> ReadPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        var permissionCodes = await dbContext.Set<Permission>()
            .AsNoTracking()
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);

        return permissionCodes.ToHashSet(StringComparer.Ordinal);
    }
}
