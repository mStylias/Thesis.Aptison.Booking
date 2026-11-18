using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Companies.Notifications;

/// <summary>
/// Drops a shop's cached home page and the sitemap when its services or staff change. Never throws:
/// a throw would make the outbox retry the availability rebuild too, and the cache expires anyway.
/// </summary>
public sealed class CompanyCatalogChangedCacheEvictionHandler(
    HybridCache cache,
    ILogger<CompanyCatalogChangedCacheEvictionHandler> logger)
    : INotificationHandler<CompanyCalendarChanged>
{
    public async Task HandleAsync(CompanyCalendarChanged notification, CancellationToken cancellationToken)
    {
        if (!notification.CatalogChanged)
        {
            return;
        }

        try
        {
            await cache.RemoveByTagAsync(
                [CompanyCacheTags.GetCompanyTag(notification.CompanyId), CompanyCacheTags.Sitemap],
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception, "Evicting the cached pages of company {CompanyId} failed", notification.CompanyId);
        }
    }
}
