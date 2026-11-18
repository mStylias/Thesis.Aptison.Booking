using Aptison.Booking.Infrastructure.Auth.Authorization;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>Caches nothing, so every resolution reads Postgres through the real store.</summary>
public sealed class PassThroughPermissionCache : IPermissionCache
{
    public bool IsAvailable => true;

    public Task<UserAccessEntry?> GetUserAccessAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<UserAccessEntry?>(null);

    public Task<IReadOnlyDictionary<Guid, string[]>> GetRolePermissionsAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string[]>>(new Dictionary<Guid, string[]>());

    public Task<long> BeginFillAsync(CancellationToken cancellationToken) => Task.FromResult(0L);

    public Task FillUserAccessAsync(
        Guid userId, UserAccessEntry access, long fillToken, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task FillRolePermissionsAsync(
        IReadOnlyDictionary<Guid, string[]> permissionsByRole, long fillToken, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
