namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Why a token Keycloak accepted could not be turned into a <c>users</c> row. The API maps these to
/// status codes: the email conflicts are actionable by the caller, the rest by an operator.
/// </summary>
public enum UserProvisioningFailure
{
    /// <summary>No <c>email</c> claim, so the realm's client scopes changed. Not the user's doing.</summary>
    EmailMissing,

    /// <summary>
    /// The address belongs to walk-ins or an unlinked row, and this token does not claim it
    /// verified. Joining anyway would hand that customer's history to whoever typed their address.
    /// </summary>
    EmailUnverifiedAndTaken,

    /// <summary>
    /// The address belongs to a row linked to a <em>different</em> account: an account was deleted
    /// and recreated, or two realms share this database.
    /// </summary>
    EmailOwnedByAnotherAccount,

    /// <summary>
    /// Neither the token's locale nor the configured default matched an active language, and
    /// <c>users.preferred_language_id</c> is NOT NULL.
    /// </summary>
    DefaultLanguageMissing,

    /// <summary>The <c>customer</c> system role is missing — seed data did not run, or was deleted.</summary>
    SystemRoleMissing,

    /// <summary>An unproven password was removed and the sessions ended. The caller signs in again.</summary>
    SessionRevoked
}

/// <summary>
/// Fatal to the request on purpose: the alternative is a caller with no <c>users.id</c>.
/// </summary>
public sealed class UserProvisioningException(UserProvisioningFailure reason, string message)
    : Exception(message)
{
    public UserProvisioningFailure Reason { get; } = reason;
}
