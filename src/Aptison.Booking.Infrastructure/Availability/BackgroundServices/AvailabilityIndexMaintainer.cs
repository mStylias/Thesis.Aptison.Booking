using Aptison.Booking.Application.Abstractions.Availability;
using Aptison.Booking.Application.Features.Availability.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Infrastructure.Availability.BackgroundServices;

/// <summary>
/// The clock half of keeping <c>company_availability_slots</c> current. Freshness is the outbox's
/// job — this covers only what no domain event can raise: the horizon moving at midnight, and
/// repairing a rebuild that dead-lettered.
/// <para>
/// It also builds on startup, because the sample data is applied by DbUp in SQL, which bypasses EF
/// and so stages nothing in the outbox.
/// </para>
/// </summary>
internal sealed class AvailabilityIndexMaintainer(
    IServiceScopeFactory scopeFactory,
    IOptions<AvailabilityIndexOptions> options,
    ILogger<AvailabilityIndexMaintainer> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly AvailabilityIndexOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastFullRebuild = DateTimeOffset.MinValue;

        if (_options.BuildOnStartup)
        {
            await RollAsync(full: true, stoppingToken);
            lastFullRebuild = timeProvider.GetUtcNow();
        }

        using var timer = new PeriodicTimer(_options.RollInterval, timeProvider);

        while (await WaitAsync(timer, stoppingToken))
        {
            var full = timeProvider.GetUtcNow() - lastFullRebuild >= _options.FullRebuildInterval;

            await RollAsync(full, stoppingToken);

            if (full)
            {
                lastFullRebuild = timeProvider.GetUtcNow();
            }
        }
    }

    private async Task RollAsync(bool full, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.SendAsync(new RollAvailabilityIndexRequest { Full = full }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Swallowed on purpose: an unhandled throw here stops the host, and a stale index is a
            // far smaller problem than an API that will not start.
            logger.LogError(exception, "The availability index roll failed (full: {Full})", full);
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
