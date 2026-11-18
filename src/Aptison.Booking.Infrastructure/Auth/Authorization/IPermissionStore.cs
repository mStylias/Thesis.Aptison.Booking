namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// The Postgres side of permission resolution — the rebuild path behind the cache, and the only
/// place these tables are read for authorization.
/// </summary>
public interface IPermissionStore
{
    /// <summary>
    /// Every active role assignment for a user, with the scope each applies in, and the company of
    /// their live, active staff row. Soft-deleted rows are excluded at build time, so a cached entry
    /// never names a role the map cannot resolve.
    /// </summary>
    Task<UserAccessEntry> ReadUserAccessAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Every active role and its permission codes, <em>including roles that grant none</em> — an
    /// empty list must be cacheable, or every request naming that role rebuilds the whole map.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string[]>> ReadRolePermissionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every permission code in the catalog. Read once at startup to check what endpoints declare.
    /// </summary>
    Task<IReadOnlySet<string>> ReadPermissionCatalogAsync(CancellationToken cancellationToken);
}
