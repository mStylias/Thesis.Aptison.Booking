namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak.Models;

/// <summary>The subset of Keycloak's <c>FederatedIdentityRepresentation</c> this client reads.</summary>
internal sealed record KeycloakFederatedIdentity(string IdentityProvider);
