using Aptison.Booking.Application.Abstractions.Auth.Authorization;

namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// The fill half of the contract <see cref="IPermissionCacheInvalidator"/> is the evict half of.
/// Every method is best-effort: a Redis outage degrades to reading Postgres per request and never
/// becomes a 500. Reads report "not cached" and "cache unavailable" identically, since the
/// resolver's response to both is to rebuild.
/// </summary>
public interface IPermissionCache : IPermissionCacheInvalidator
{
    /// <summary>
    /// A user's cached access, or <c>null</c> if there is no entry. No grants and no staff company
    /// is a real answer — a plain customer — and is cached as one.
    /// </summary>
    Task<UserAccessEntry?> GetUserAccessAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The permission codes of the named roles. A role with no cached entry is absent from the
    /// result rather than present-and-empty; the caller rebuilds and refills.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string[]>> GetRolePermissionsAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the token a subsequent fill is checked against. Call it <em>before</em> reading
    /// Postgres, or it cannot notice an invalidation landing during that read.
    /// </summary>
    Task<long> BeginFillAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records a user's access, dropping the write if anything was invalidated since
    /// <paramref name="fillToken"/> was taken. Nothing here expires, so a stale write is permanent.
    /// </summary>
    Task FillUserAccessAsync(
        Guid userId, UserAccessEntry access, long fillToken, CancellationToken cancellationToken);

    /// <summary>Records the role map, subject to the same guard as <see cref="FillUserAccessAsync"/>.</summary>
    Task FillRolePermissionsAsync(
        IReadOnlyDictionary<Guid, string[]> permissionsByRole, long fillToken, CancellationToken cancellationToken);

    /// <summary>Whether the cache is currently reachable. Read by the health check.</summary>
    bool IsAvailable { get; }
}
