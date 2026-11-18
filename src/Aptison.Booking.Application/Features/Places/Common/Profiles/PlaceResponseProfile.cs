using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Application.Features.Places.Common.Responses;
using Aptison.Booking.Domain.Reference;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Places.Common.Profiles;

/// <summary>
/// Provider place → suggestion. The one place that decides how a street, a city and an airport
/// read, so every consumer agrees and a provider change stays invisible.
/// </summary>
/// <remarks>
/// Bump <c>PlaceSearchOptions.CompositionVersion</c> when any of this changes, or cached answers
/// keep the old wording until they expire.
/// </remarks>
public class PlaceResponseProfile : MapperProfile
{
    public PlaceResponseProfile()
    {
        CreateMap<ProviderPlace, PlaceSuggestionResponse>()
            .For(source => source.Id, source => GetId(source))
            .For(source => source.Label, source => source.Name)
            .For(source => source.Context, source => GetContext(source))
            .For(source => source.CountryCode, source => source.CountryCode.ToUpperInvariant())
            .For(source => source.Street, source => GetStreet(source));
    }

    /// <summary>
    /// The disambiguating line, coarsest part last, with anything already in the label left out —
    /// a city's own name would otherwise open its own context.
    /// </summary>
    private static string GetContext(ProviderPlace place)
    {
        var parts = new List<string>(3);

        foreach (var part in new[] { place.City, place.Region, place.Country })
        {
            if (!string.IsNullOrWhiteSpace(part)
                && !Matches(part, place.Name)
                && !parts.Any(existing => Matches(part, existing)))
            {
                parts.Add(part.Trim());
            }
        }

        return string.Join(", ", parts);
    }

    private static string? GetStreet(ProviderPlace place) =>
        place.Type is PlaceTypes.Street or PlaceTypes.Address ? place.Name : null;

    private static bool Matches(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Derived from the place itself, so the same answer keys the same row on every request. It is
    /// a list key and nothing more — see <see cref="PlaceSuggestionResponse"/>.
    /// </summary>
    private static string GetId(ProviderPlace place)
    {
        var material = string.Create(
            CultureInfo.InvariantCulture,
            $"{place.Type}|{place.Latitude:F6}|{place.Longitude:F6}|{place.Name}");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return Base64Url.EncodeToString(hash.AsSpan(0, 16));
    }
}
