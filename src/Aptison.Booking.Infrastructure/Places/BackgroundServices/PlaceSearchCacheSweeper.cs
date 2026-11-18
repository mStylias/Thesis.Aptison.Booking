using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Places;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Infrastructure.Places.BackgroundServices;

/// <summary>
/// Deletes expired place-search answers. Nothing else bounds the table: entries are written by an
/// anonymous public endpoint and read only by an exact key, so an unswept row is invisible and
/// permanent.
/// </summary>
internal sealed class PlaceSearchCacheSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<PlaceSearchOptions> options,
    TimeProvider timeProvider,
    ILogger<PlaceSearchCacheSweeper> logger) : BackgroundService
{
    private readonly PlaceSearchOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CacheSweepInterval, timeProvider);

        // Waits one interval first, so the first pass does not race the migrations.
        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The place-search cache sweep failed; retrying next cycle");
            }
        }
    }

    private async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<BookingContext>>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var deleted = await unitOfWork.GetRepository<PlaceSearchCacheEntry>()
            .ExecuteHardDeleteAsync(
                query => query.Where(entry => entry.ExpiresAt <= now), cancellationToken);

        logger.LogInformation("Swept {DeletedCount} expired place-search cache entries", deleted);
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
