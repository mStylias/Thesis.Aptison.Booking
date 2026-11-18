namespace Aptison.Booking.Application.Abstractions.Places;

/// <summary>
/// Search policy and cache lifetimes. Every knob here is configuration so cost can be tuned
/// without a front-end release — the client never sends a limit or a country.
/// </summary>
public sealed class PlaceSearchOptions
{
    public const string SectionName = "PlaceSearch";

    /// <summary>Shorter queries are a 400, never an empty result.</summary>
    public int MinimumQueryLength { get; set; } = 2;

    /// <summary>
    /// Longer queries are truncated rather than refused: the contract names no maximum, and an
    /// unbounded query is an unbounded spend on a provider billed per request.
    /// </summary>
    public int MaximumQueryLength { get; set; } = 120;

    public int MaxSuggestions { get; set; } = 5;

    /// <summary>
    /// ISO 3166-1 alpha-2. Empty searches everywhere. The opening policy of Greece lives in
    /// <c>appsettings.json</c>, not here: the configuration binder <em>appends</em> to a non-empty
    /// array default, so a default here would survive every attempt to configure it away.
    /// </summary>
    public string[] Countries { get; set; } = [];

    /// <summary>Provider place kinds to keep. Empty means all of them.</summary>
    public string[] Types { get; set; } = [];

    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Shorter than <see cref="CacheLifetime"/>: a place that does not exist yet might.</summary>
    public TimeSpan EmptyResultCacheLifetime { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// How coarsely a bias point is rounded before it enters the cache key. Exact coordinates
    /// would give every visitor their own entry and drive the hit rate to zero. 0.1° is roughly
    /// 11 km.
    /// </summary>
    public double CacheCellDegrees { get; set; } = 0.1;

    public TimeSpan CacheSweepInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Bumped by hand when the composition of <c>label</c>, <c>context</c> or <c>type</c> changes.
    /// It joins the policy into the cache key, so pre-change answers are never served.
    /// </summary>
    public int CompositionVersion { get; set; } = 1;
}
