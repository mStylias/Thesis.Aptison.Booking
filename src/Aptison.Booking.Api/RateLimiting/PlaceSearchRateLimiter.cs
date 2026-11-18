using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Aptison.Booking.Api.RateLimiting;

/// <summary>
/// The per-caller ceiling on place search. Every call the cache does not answer is billed by the
/// provider, so this is the cost floor rather than a nicety.
/// </summary>
public static class PlaceSearchRateLimiter
{
    public const string PolicyName = "place-search";

    public static IServiceCollection AddPlaceSearchRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PlaceSearchRateLimitOptions.SectionName);
        services.Configure<PlaceSearchRateLimitOptions>(section);

        var limits = section.Get<PlaceSearchRateLimitOptions>() ?? new PlaceSearchRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetTokenBucketLimiter(
                GetCallerKey(context),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = limits.Burst,
                    TokensPerPeriod = limits.SearchesPerSecond,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    AutoReplenishment = true,

                    // Refused immediately rather than held: a queued typeahead answers for text the
                    // visitor has already replaced.
                    QueueLimit = 0
                }));

            options.OnRejected = RateLimitRejection.WriteProblemAsync;
        });

        return services;
    }

    /// <summary>
    /// The address the connection came from — which is the real caller's only once
    /// <c>UseRealClientAddress</c> has run. A caller with no address shares one bucket, which is
    /// the safe direction.
    /// </summary>
    private static string GetCallerKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
