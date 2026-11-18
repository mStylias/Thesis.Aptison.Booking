using System.Security.Claims;

namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// The claims provisioning needs, lifted off the request principal so that
/// <see cref="IUserProvisioner"/> takes no dependency on HTTP. <c>Subject</c> is the only stable
/// one; the rest are profile details the user can change.
/// </summary>
public sealed record AuthenticatedSubject(
    Guid Subject,
    string? Email,
    bool EmailVerified,
    string? FirstName,
    string? LastName,
    string? Locale,
    DateTimeOffset? IssuedAt = null)
{
    /// <summary>
    /// The one place these claim names are written down. Raw OIDC names, because the JWT handler
    /// runs with <c>MapInboundClaims = false</c> — under the default mapping every lookup here
    /// silently finds nothing.
    /// </summary>
    public static AuthenticatedSubject FromPrincipal(Guid subject, ClaimsPrincipal? principal) =>
        new(
            Subject: subject,
            Email: principal?.FindFirst("email")?.Value,
            // Absent reads as false: it gates record adoption and reaching any endpoint at all.
            EmailVerified: bool.TryParse(principal?.FindFirst("email_verified")?.Value, out var verified)
                           && verified,
            FirstName: principal?.FindFirst("given_name")?.Value,
            LastName: principal?.FindFirst("family_name")?.Value,
            Locale: principal?.FindFirst("locale")?.Value,
            IssuedAt: long.TryParse(principal?.FindFirst("iat")?.Value, out var issuedAt)
                ? DateTimeOffset.FromUnixTimeSeconds(issuedAt)
                : null);
}
