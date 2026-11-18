namespace Aptison.Booking.Api.Cors;

/// <summary>
/// Browser origins permitted to call this API. Unrelated to the Keycloak clients'
/// <c>webOrigins</c>, which govern the browser's calls to Keycloak — configuring one and assuming
/// it covers the other is the usual cause of a working login followed by failed requests.
/// </summary>
public sealed class ClientCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>The name of the single policy this API applies to every endpoint.</summary>
    public const string PolicyName = "ClientApps";

    /// <summary>
    /// Exact origins (scheme, host and port, no trailing slash). Must agree with the
    /// <c>koureia-spa</c> Keycloak client's, or sign-in completes and the first API call fails.
    /// Empty means no browser may call this API, and is the default outside Development so a
    /// deployment names its own origins. Native clients need no entry.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
