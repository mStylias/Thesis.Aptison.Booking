using System.Text.Json.Serialization;
using Aptison.Booking.Application.Common.Location;

namespace Aptison.Booking.Infrastructure.Places.Models;

/// <summary>
/// One PlaceKit record. Every member is optional in practice, whatever the reference says, so
/// nothing here is required.
/// </summary>
internal sealed record PlaceKitRecord
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("county")]
    public string? County { get; init; }

    /// <summary>PlaceKit's word for the region.</summary>
    [JsonPropertyName("administrative")]
    public string? Administrative { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("countrycode")]
    public string? CountryCode { get; init; }

    [JsonPropertyName("zipcode")]
    public string[]? ZipCode { get; init; }

    /// <summary>Latitude and longitude, in that order, as one comma-separated string.</summary>
    [JsonPropertyName("coordinates")]
    public string? Coordinates { get; init; }

    [JsonPropertyName("lat")]
    public double? Lat { get; init; }

    [JsonPropertyName("lng")]
    public double? Lng { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// The position, from <see cref="Coordinates"/> or the deprecated pair PlaceKit still sends.
    /// Null means the record cannot be offered as an origin, which is the one reason to drop one.
    /// </summary>
    [JsonIgnore]
    public (double Latitude, double Longitude)? Position =>
        LocationUtils.GetCoordinates(Coordinates)
        ?? (this is { Lat: { } latitude, Lng: { } longitude } ? (latitude, longitude) : null);
}
