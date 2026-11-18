using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Domain.Authorization;
using Microsoft.Extensions.Logging;

namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// Reads the two cached structures, applies scope, and unions what survives. The scope filter below
/// is the single place company scope is enforced — flattening a user's roles across companies would
/// grant their highest permission everywhere.
/// </summary>
internal sealed class PermissionResolver(
    IPermissionCache cache,
    IPermissionStore store,
    ILogger<PermissionResolver> logger) : IPermissionResolver
{
    public async Task<ResolvedPermissions> ResolveAsync(
        Guid userId, Guid? companyId, CancellationToken cancellationToken) =>
        await ResolveAsync(await GetAccessAsync(userId, cancellationToken), companyId, cancellationToken);

    /// <summary>
    /// A grant counts only where it was made: global grants at platform scope, and grants at the
    /// company when one is set. A global grant is absent at a company, not merely weaker.
    /// </summary>
    public async Task<ResolvedPermissions> ResolveAsync(
        UserAccessEntry access, Guid? companyId, CancellationToken cancellationToken)
    {
        var scopedRoleIds = GetRoleIds(access, companyId);
        var globalRoleIds = companyId is null ? scopedRoleIds : GetRoleIds(access, companyId: null);

        Guid[] roleIds = [.. scopedRoleIds.Union(globalRoleIds)];

        if (roleIds.Length == 0)
        {
            return ResolvedPermissions.None;
        }

        var permissionsByRole = await GetRolePermissionsAsync(roleIds, cancellationToken);

        return new ResolvedPermissions(
            GetCodes(scopedRoleIds, permissionsByRole),
            GetCodes(globalRoleIds, permissionsByRole).Contains(PermissionCodes.Platform.Manage));
    }

    public async Task<UserAccessEntry> GetAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await cache.GetUserAccessAsync(userId, cancellationToken) is { } cachedAccess)
        {
            return cachedAccess;
        }

        var fillToken = await cache.BeginFillAsync(cancellationToken);
        var access = await store.ReadUserAccessAsync(userId, cancellationToken);
        await cache.FillUserAccessAsync(userId, access, fillToken, cancellationToken);

        logger.LogDebug("Rebuilt access for user {UserId} from Postgres", userId);

        return access;
    }

    private static Guid[] GetRoleIds(UserAccessEntry access, Guid? companyId) =>
    [
        .. access.Grants
            .Where(grant => grant.OwnerId == companyId)
            .Select(grant => grant.RoleId)
            .Distinct()
    ];

    private static HashSet<string> GetCodes(
        IEnumerable<Guid> roleIds, IReadOnlyDictionary<Guid, string[]> permissionsByRole)
    {
        var effectiveCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleId in roleIds)
        {
            if (permissionsByRole.TryGetValue(roleId, out var permissionCodes))
            {
                effectiveCodes.UnionWith(permissionCodes);
            }

            // A role still missing after the rebuild was deleted since the access entry was cached.
            // The invalidation that removed it also dropped this user's entry, so ignore it.
        }

        return effectiveCodes;
    }

    private async Task<IReadOnlyDictionary<Guid, string[]>> GetRolePermissionsAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var cachedPermissions = await cache.GetRolePermissionsAsync(roleIds, cancellationToken);

        if (cachedPermissions.Count == roleIds.Count)
        {
            return cachedPermissions;
        }

        // Rebuild the whole map rather than the gap: it is one small global structure, and this
        // leaves it complete for the next request.
        var fillToken = await cache.BeginFillAsync(cancellationToken);
        var rebuiltPermissions = await store.ReadRolePermissionsAsync(cancellationToken);
        await cache.FillRolePermissionsAsync(rebuiltPermissions, fillToken, cancellationToken);

        logger.LogDebug("Rebuilt the role permission map from Postgres ({RoleCount} roles)", rebuiltPermissions.Count);

        return rebuiltPermissions;
    }
}
