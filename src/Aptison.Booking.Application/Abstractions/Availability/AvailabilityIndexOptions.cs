namespace Aptison.Booking.Application.Abstractions.Availability;

/// <summary>
/// How far ahead the availability index is materialized, and how often the maintenance pass runs.
/// Freshness comes from domain events, not from here — see the roll's own summary.
/// </summary>
public sealed class AvailabilityIndexOptions
{
    public const string SectionName = "AvailabilityIndex";

    /// <summary>
    /// Days of slots to hold. A company's own <c>max_advance_days</c> still caps it, so this is a
    /// ceiling rather than a target, and it is the main lever on the table's size.
    /// </summary>
    public int HorizonDays { get; set; } = 60;

    public bool BuildOnStartup { get; set; } = true;

    /// <summary>How often the maintenance pass runs; a day's worth of horizon enters per pass.</summary>
    public TimeSpan RollInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// How often the pass rebuilds everything instead of only the entering day. This is the repair
    /// path for a rebuild that dead-lettered, which nothing else would ever fix.
    /// </summary>
    public TimeSpan FullRebuildInterval { get; set; } = TimeSpan.FromDays(7);
}
