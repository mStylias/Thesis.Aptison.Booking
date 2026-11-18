namespace Aptison.Booking.Application.Features.Places.Common.Responses;

/// <summary>
/// One place a visitor can choose as their origin. <c>Label</c> and <c>Context</c> are composed
/// here rather than by the client, so every consumer reads a street, a city and an airport the
/// same way and a provider change stays invisible. No profile — nothing maps from a table.
/// </summary>
/// <remarks>
/// <c>Id</c> is opaque and lives only as long as the cached answer. It keys a suggestion list and
/// nothing else; a chosen place travels onward as its coordinates.
/// </remarks>
public record PlaceSuggestionResponse(
    string Id,
    string Label,
    string Context,
    string Type,
    double Latitude,
    double Longitude,
    string CountryCode,
    string? City,
    string? Region,
    string? Street,
    string? PostalCode);
