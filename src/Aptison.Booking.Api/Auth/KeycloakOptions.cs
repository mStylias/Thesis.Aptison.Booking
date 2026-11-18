namespace Aptison.Booking.Api.Auth;

/// <summary>
/// Keycloak bearer-token settings, bound from the <c>Keycloak</c> configuration section.
/// </summary>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>
    /// The realm URL OpenID metadata and signing keys are fetched from, reachable from wherever the
    /// API runs — by service name in the container, by localhost on the host.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>The expected <c>aud</c> claim.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Every issuer a valid token may carry. Keycloak stamps <c>iss</c> with the hostname the token
    /// was requested through, so the same realm issues different ones inside and outside compose.
    /// </summary>
    public string[] ValidIssuers { get; set; } = [];

    /// <summary>False for local development, where Keycloak is served over plain HTTP.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
