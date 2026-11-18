namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak;

/// <summary>
/// How to reach the identity provider's Admin API. Separate from the host's own <c>Keycloak</c>
/// settings, which describe token <em>validation</em>; this is outbound, write-capable, and carries
/// a secret.
/// </summary>
public sealed class KeycloakAdminOptions
{
    public const string SectionName = "Keycloak:Admin";

    /// <summary>
    /// Server root, no realm path, no trailing slash — the client builds both
    /// <c>/realms/{realm}/...</c> and <c>/admin/realms/{realm}/...</c> off it.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Realm { get; set; } = string.Empty;

    /// <summary>The confidential client whose service account administers.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>A credential; the committed appsettings leave it empty on purpose.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>How far before stated expiry to stop reusing a token.</summary>
    public TimeSpan TokenExpiryLeeway { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
