namespace Aptison.Booking.Application.Abstractions.Companies;

/// <summary>
/// Knobs for the public company browse. The origin is a setting rather than a literal so a caller
/// that knows where it is only has to send coordinates.
/// </summary>
public sealed class CompanyBrowseOptions
{
    public const string SectionName = "CompanyBrowse";

    /// <summary>
    /// Athens centre. The fallback origin when the caller sends no coordinates, and what
    /// <c>/all</c> always measures from.
    /// </summary>
    // 37.97157785594371, 23.725726869039587 is Acropolis.
    // A caller with an origin sends it as latitude/longitude; see the location-search spec in the
    // Koureia.Web repo. Radius filtering is deferred there, so accuracy stays out of this endpoint.
    public double DefaultLatitude { get; set; } = 37.97157785594371;

    /// <inheritdoc cref="DefaultLatitude"/>
    public double DefaultLongitude { get; set; } = 23.725726869039587;

    /// <summary>How long the whole-list answer stays in Redis, shared by every instance.</summary>
    public TimeSpan AllCacheTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long each instance keeps its own in-process copy. Shorter than <see cref="AllCacheTtl"/>
    /// because nothing evicts it across instances — this is the window in which two servers can
    /// disagree about the list.
    /// </summary>
    public TimeSpan AllCacheLocalTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The same pair for a shop's home page and the sitemap list. A tag eviction reaches Redis and
    /// this instance only, so another instance can serve its local copy for up to
    /// <see cref="PageCacheLocalTtl"/>.
    /// </summary>
    public TimeSpan PageCacheTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <inheritdoc cref="PageCacheTtl"/>
    public TimeSpan PageCacheLocalTtl { get; set; } = TimeSpan.FromMinutes(1);
}
