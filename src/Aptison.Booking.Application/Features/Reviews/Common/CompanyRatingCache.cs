using Aptison.Booking.Application.Features.Companies.Common;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Aptison.Booking.Application.Features.Reviews.Common;

internal static class CompanyRatingCache
{
    /// <summary>
    /// Drops the shop's cached home page, which carries its rating. After the commit, and never
    /// throws: the review is saved, and the page expires on its own anyway.
    /// </summary>
    public static async Task EvictAsync(
        HybridCache cache, Guid companyId, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveByTagAsync(CompanyCacheTags.GetCompanyTag(companyId), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Evicting the cached page of company {CompanyId} failed", companyId);
        }
    }
}
