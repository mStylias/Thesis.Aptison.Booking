using System.Text.Json.Serialization;

namespace Aptison.Booking.Infrastructure.Places.Contracts;

/// <summary>PlaceKit's <c>POST /search</c> body. Null members are omitted by the serializer.</summary>
internal sealed record PlaceKitSearchRequest
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("countries")]
    public string[]? Countries { get; init; }

    [JsonPropertyName("types")]
    public string[]? Types { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; init; }

    /// <summary>Latitude and longitude, in that order, as one comma-separated string.</summary>
    [JsonPropertyName("coordinates")]
    public string? Coordinates { get; init; }
}
