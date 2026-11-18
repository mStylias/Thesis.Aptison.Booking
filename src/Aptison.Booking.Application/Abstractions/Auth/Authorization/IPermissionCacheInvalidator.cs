namespace Aptison.Booking.Application.Abstractions.Auth.Authorization;

/// <summary>
/// Evicts the cached structures permission resolution reads. Every write to <c>roles</c>,
/// <c>role_permissions</c> or <c>user_roles</c> must call one of these, or revocation stops being
/// immediate.
/// <para>
/// <b>Call after the transaction commits, never inside it.</b> Evicting first lets a concurrent
/// request refill from a snapshot that predates the change, and nothing here expires.
/// </para>
/// </summary>
public interface IPermissionCacheInvalidator
{
    /// <summary>
    /// A role's permissions changed, or it was created or soft-deleted. Affects every holder and
    /// costs one entry, which is why the cache stores <c>role → permissions</c> unflattened.
    /// </summary>
    Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>A user's assignments changed — granted, revoked, or scope altered.</summary>
    Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A change reaching more users than are worth enumerating — a widely-held role deleted, or a
    /// bulk import.
    /// </summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
