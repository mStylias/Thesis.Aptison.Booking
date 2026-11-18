namespace Aptison.Booking.Api.RateLimiting;

/// <summary>
/// How fast one caller may search for places.
/// </summary>
/// <remarks>
/// Chosen against the 340 ms the control settles for before it sends: a person typing without
/// pause produces 2.94 searches a second, so three is just above what one honest typist can
/// generate. The burst covers a first keystroke, a correction and a locate action landing together.
/// <para>
/// Counted in this process. With more than one instance behind a load balancer the real ceiling is
/// this many per instance, so the count would have to move to Redis.
/// </para>
/// </remarks>
public sealed class PlaceSearchRateLimitOptions
{
    public const string SectionName = "PlaceSearchRateLimit";

    public int SearchesPerSecond { get; set; } = 3;

    public int Burst { get; set; } = 5;
}
