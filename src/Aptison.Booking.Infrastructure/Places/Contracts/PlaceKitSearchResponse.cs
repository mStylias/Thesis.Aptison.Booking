using System.Text.Json.Serialization;
using Aptison.Booking.Infrastructure.Places.Models;

namespace Aptison.Booking.Infrastructure.Places.Contracts;

internal sealed record PlaceKitSearchResponse
{
    [JsonPropertyName("results")]
    public PlaceKitRecord[] Results { get; init; } = [];
}
