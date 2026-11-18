using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Infrastructure.Auth.Authorization.BackgroundServices;

/// <summary>
/// Builds the role map at startup so the first requests do not race to build it. Only the global
/// structure; per-user entries are filled on first sight. Failure is logged, not thrown — a cold
/// cache is a slower API, not a broken one.
/// </summary>
internal sealed class PermissionCacheWarmer(
    IServiceScopeFactory scopeFactory,
    IPermissionCache cache,
    IOptions<AuthorizationCacheOptions> options,
    ILogger<PermissionCacheWarmer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.WarmOnStartup)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IPermissionStore>();

            var fillToken = await cache.BeginFillAsync(cancellationToken);
            var permissionsByRole = await store.ReadRolePermissionsAsync(cancellationToken);
            await cache.FillRolePermissionsAsync(permissionsByRole, fillToken, cancellationToken);

            logger.LogInformation(
                "Warmed the permission cache with {RoleCount} roles", permissionsByRole.Count);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(
                e, "Could not warm the permission cache. It will be built by the first request that misses");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
