using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Application.Common.StringOperations;

namespace Aptison.Booking.Application.Features.Places.Common;

/// <summary>
/// Turns a search into the one string the cache table is keyed by.
/// </summary>
internal static class PlaceSearchCacheKey
{
    /// <summary>
    /// Hashed, so the table holds no typed text. The policy goes in too: change the countries,
    /// the types, the result count or the composition and pre-change answers stop being served
    /// rather than being served wrongly.
    /// </summary>
    public static string Create(
        string normalizedQuery,
        string? languageCode,
        double? latitude,
        double? longitude,
        PlaceSearchOptions options)
    {
        var material = string.Join(
            '\n',
            GetPolicy(options),
            languageCode ?? "-",
            GetCell(latitude, longitude, options.CacheCellDegrees),
            normalizedQuery);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>Trimmed, folded to lower case, with runs of whitespace collapsed to one space.</summary>
    public static string Normalize(string query) =>
        StringUtils.ToLowerCase(
            string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

    /// <summary>
    /// The bias point rounded to a coarse cell. Exact coordinates would give every visitor their
    /// own entry and drive the hit rate to zero.
    /// </summary>
    private static string GetCell(double? latitude, double? longitude, double cellDegrees)
    {
        if (latitude is not { } lat || longitude is not { } lng)
        {
            return "-";
        }

        var latCell = (long)Math.Floor(lat / cellDegrees);
        var lngCell = (long)Math.Floor(lng / cellDegrees);

        return string.Create(CultureInfo.InvariantCulture, $"{latCell},{lngCell}");
    }

    private static string GetPolicy(PlaceSearchOptions options) => string.Join(
        '/',
        options.CompositionVersion.ToString(CultureInfo.InvariantCulture),
        options.MaxSuggestions.ToString(CultureInfo.InvariantCulture),
        options.MinimumQueryLength.ToString(CultureInfo.InvariantCulture),
        string.Join(',', options.Countries.Order(StringComparer.OrdinalIgnoreCase)),
        string.Join(',', options.Types.Order(StringComparer.OrdinalIgnoreCase)));
}
