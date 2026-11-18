using Aptison.Booking.Domain;
using Synax.AspNetCore.HealthChecks;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Infrastructure.HealthChecks;

/// <summary>
/// Discovered and registered by <c>builder.AddHealthChecks()</c> in
/// <see cref="BookingInfrastructureModule"/> — no manual <c>AddCheck&lt;&gt;()</c> needed.
/// </summary>
public class PostgreSqlDatabaseHealthCheck(IDatabaseManager<BookingContext> databaseManager) : ISynaxHealthCheck
{
    public string Name => "PostgreSQL";

    public async Task<SynaxHealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var canConnect = await databaseManager.CanConnectAsync(cancellationToken);

        return canConnect
            ? SynaxHealthCheckResult.Healthy(Name, $"Provider: {databaseManager.ProviderName}")
            : SynaxHealthCheckResult.Unhealthy(Name, "Cannot connect to the database.");
    }
}
