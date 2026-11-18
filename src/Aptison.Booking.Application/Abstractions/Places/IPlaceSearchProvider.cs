namespace Aptison.Booking.Application.Abstractions.Places;

/// <summary>
/// The external geocoder. Implementations translate their own vocabulary into
/// <see cref="ProviderPlace"/>, so swapping provider changes nothing above this line.
/// </summary>
public interface IPlaceSearchProvider
{
    Task<PlaceSearchProviderResult> SearchAsync(
        PlaceSearchProviderQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// <paramref name="Latitude"/> and <paramref name="Longitude"/> bias results toward a point; they
/// arrive together or not at all.
/// </summary>
public sealed record PlaceSearchProviderQuery(
    string Query,
    string? LanguageCode,
    double? Latitude,
    double? Longitude);

/// <summary>
/// One place, already in this API's vocabulary. <c>Type</c> is a <c>PlaceTypes</c> constant.
/// </summary>
public sealed record ProviderPlace(
    string Name,
    string Type,
    double Latitude,
    double Longitude,
    string CountryCode,
    string? City,
    string? County,
    string? Region,
    string? Country,
    string? PostalCode);

/// <summary>What <see cref="IPlaceSearchProvider.SearchAsync"/> did.</summary>
public abstract record PlaceSearchProviderResult
{
    private PlaceSearchProviderResult() { }

    /// <summary>Answered. An empty list is a real answer: nothing matched.</summary>
    public sealed record Found(IReadOnlyList<ProviderPlace> Places) : PlaceSearchProviderResult;

    /// <summary>
    /// Unreachable, out of quota, or the credential was rejected. All three are the capability
    /// being down rather than the caller being wrong, and all three are retryable.
    /// </summary>
    public sealed record Unavailable(string Reason) : PlaceSearchProviderResult;
}
