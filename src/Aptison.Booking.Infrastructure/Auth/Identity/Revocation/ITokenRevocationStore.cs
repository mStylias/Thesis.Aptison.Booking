namespace Aptison.Booking.Infrastructure.Auth.Identity.Revocation;

/// <summary>
/// Refuses the access tokens a subject held when its sessions were ended. Keycloak ends the
/// sessions, but this API checks tokens locally, so a token already issued works until it expires.
/// </summary>
internal interface ITokenRevocationStore
{
    /// <summary>Refuses every token of the subject issued at or before <paramref name="cutoff"/>.</summary>
    Task RevokeIssuedUntilAsync(Guid subject, DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Whether a token issued at <paramref name="issuedAt"/> is refused.</summary>
    Task<bool> IsRevokedAsync(Guid subject, DateTimeOffset issuedAt, CancellationToken cancellationToken);
}
