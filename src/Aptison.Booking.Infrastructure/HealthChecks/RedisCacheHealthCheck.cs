using Aptison.Booking.Infrastructure.Auth.Authorization;
using Synax.AspNetCore.HealthChecks;

namespace Aptison.Booking.Infrastructure.HealthChecks;

/// <summary>
/// Reports Redis as <em>degraded</em>, not unhealthy: permission resolution reads through to
/// Postgres, so the answers stay correct. Unhealthy would pull the instance out of rotation for a
/// condition it can serve through; healthy would hide the hot path moving onto the database.
/// </summary>
public class RedisCacheHealthCheck(IPermissionCache cache) : ISynaxHealthCheck
{
    public string Name => "Redis";

    public Task<SynaxHealthCheckResult> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(cache.IsAvailable
            ? SynaxHealthCheckResult.Healthy(Name, "Permission cache reachable.")
            : SynaxHealthCheckResult.Degraded(
                Name, "Permission cache unreachable; resolving permissions from PostgreSQL."));
}
