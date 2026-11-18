namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak.Models;

/// <summary>The subset of Keycloak's <c>UserRepresentation</c> this client reads.</summary>
internal sealed record KeycloakUser(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    bool EmailVerified,
    long CreatedTimestamp,
    string[]? RequiredActions,
    Dictionary<string, string[]>? Attributes)
{
    /// <summary>
    /// Backs the <c>locale</c> claim and Keycloak's own login pages; only honoured while the realm
    /// has internationalization enabled.
    /// </summary>
    public const string LocaleAttribute = "locale";

    /// <summary>Absent on a <c>briefRepresentation</c>, so a caller needing it asks for the full one.</summary>
    public string? PreferredLanguageCode =>
        Attributes?.GetValueOrDefault(LocaleAttribute)?.FirstOrDefault();
}
