namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak.Models;

/// <summary>The subset of Keycloak's <c>CredentialRepresentation</c> this client reads.</summary>
internal sealed record KeycloakCredential(string Id, string Type)
{
    public const string PasswordType = "password";
}
