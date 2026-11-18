using Aptison.Booking.Application.Abstractions.Auth.Authorization;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Records the evictions a handler asked for. Nothing enforces that a write to <c>roles</c>,
/// <c>role_permissions</c> or <c>user_roles</c> invalidates the cache, and forgetting produces no
/// symptom until someone's revoked access still works — so a test asserting on these lists is the
/// only check there is.
/// </summary>
public sealed class FakePermissionCacheInvalidator : IPermissionCacheInvalidator
{
    public List<Guid> InvalidatedRolesIds { get; } = [];

    public List<Guid> InvalidatedUsersIds { get; } = [];

    public int InvalidateAllCount { get; private set; }

    public Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        InvalidatedRolesIds.Add(roleId);
        return Task.CompletedTask;
    }

    public Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        InvalidatedUsersIds.Add(userId);
        return Task.CompletedTask;
    }

    public Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        InvalidateAllCount++;
        return Task.CompletedTask;
    }
}
