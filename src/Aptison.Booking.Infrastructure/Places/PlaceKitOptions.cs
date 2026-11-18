namespace Aptison.Booking.Infrastructure.Places;

/// <summary>
/// How to reach PlaceKit. What is searched for is <c>PlaceSearchOptions</c>; this is only the
/// connection.
/// </summary>
public sealed class PlaceKitOptions
{
    public const string SectionName = "PlaceKit";

    public string BaseUrl { get; set; } = "https://api.placekit.co";

    /// <summary>
    /// Server-side only. It must never appear in configuration a browser or a build artifact can
    /// reach — the whole reason the provider is called from here rather than from the page.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Short on purpose: a typeahead that answers late has already failed the visitor, and the
    /// timeout is reported as the capability being unavailable rather than as no matches.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
