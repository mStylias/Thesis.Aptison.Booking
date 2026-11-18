using System.Diagnostics.Metrics;
using Aptison.Booking.Application.Common.Observability;

namespace Aptison.Booking.Application.Features.Places.Common;

/// <summary>
/// What the cost model is watched through: cache hit rate, provider call volume — which is the
/// bill — provider error rate and provider latency.
/// </summary>
internal static class PlaceSearchMetrics
{
    private static readonly Meter Meter = new(Telemetry.PlaceSearchMeterName);

    private static readonly Counter<long> Searches =
        Meter.CreateCounter<long>("place_search.searches", "{search}", "Accepted place searches.");

    private static readonly Counter<long> ProviderCalls = Meter.CreateCounter<long>(
        "place_search.provider.calls", "{call}", "Calls to the geocoding provider — the billed unit.");

    private static readonly Histogram<double> ProviderDuration = Meter.CreateHistogram<double>(
        "place_search.provider.duration", "ms", "How long the geocoding provider took to answer.");

    public static void RecordCacheHit() => Searches.Add(1, new KeyValuePair<string, object?>("source", "cache"));

    public static void RecordCacheMiss() => Searches.Add(1, new KeyValuePair<string, object?>("source", "provider"));

    public static void RecordProviderCall(string outcome, TimeSpan elapsed)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome);

        ProviderCalls.Add(1, tag);
        ProviderDuration.Record(elapsed.TotalMilliseconds, tag);
    }
}
