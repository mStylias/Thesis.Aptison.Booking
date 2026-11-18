using Aptison.Booking.Application.Abstractions.Places;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Returns <see cref="Places"/> for every query, or reports the geocoder down when
/// <see cref="UnavailableReason"/> is set. Records the queries so a test can assert what was asked
/// and, more usefully, that a cache hit asked nothing.
/// </summary>
public sealed class FakePlaceSearchProvider : IPlaceSearchProvider
{
    public List<ProviderPlace> Places { get; } = [];

    public string? UnavailableReason { get; set; }

    public List<PlaceSearchProviderQuery> Queries { get; } = [];

    public Task<PlaceSearchProviderResult> SearchAsync(
        PlaceSearchProviderQuery query, CancellationToken cancellationToken)
    {
        Queries.Add(query);

        PlaceSearchProviderResult result = UnavailableReason is { } reason
            ? new PlaceSearchProviderResult.Unavailable(reason)
            : new PlaceSearchProviderResult.Found(Places.ToList());

        return Task.FromResult(result);
    }
}
