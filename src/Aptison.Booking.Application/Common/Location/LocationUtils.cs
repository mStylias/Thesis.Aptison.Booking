using System.Globalization;

namespace Aptison.Booking.Application.Common.Location;

/// <summary>
/// Latitude and longitude as text and as bounds, in the order every geocoder writes them:
/// latitude first.
/// </summary>
public static class LocationUtils
{
    /// <summary>
    /// Reads a <c>"37.9795, 23.7442"</c> pair. Null when the text is missing or will not parse —
    /// a caller decides whether that is a dropped record or an error.
    /// </summary>
    public static (double Latitude, double Longitude)? GetCoordinates(string? text)
    {
        var parts = text?.Split(',');

        if (parts is not { Length: 2 })
        {
            return null;
        }

        return double.TryParse(parts[0], CultureInfo.InvariantCulture, out var latitude)
               && double.TryParse(parts[1], CultureInfo.InvariantCulture, out var longitude)
            ? (latitude, longitude)
            : null;
    }

    /// <summary>Invariant, because a comma decimal separator would split the pair in two.</summary>
    public static string FormatCoordinates(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{latitude},{longitude}");

    public static bool IsValidLatitude(double? latitude) => latitude is null or (>= -90 and <= 90);

    public static bool IsValidLongitude(double? longitude) => longitude is null or (>= -180 and <= 180);

    /// <summary>A point is given as a pair or not at all; one half of one is a bad request.</summary>
    public static bool IsCompletePoint(double? latitude, double? longitude) =>
        latitude.HasValue == longitude.HasValue;
}
