namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak;

/// <summary>
/// Why a call to the identity provider's Admin API could not be completed. The members have
/// different audiences: some are operator faults no caller can act on, others are facts about the
/// caller's own account.
/// </summary>
public enum KeycloakAdminFailure
{
    /// <summary>
    /// <c>401</c>/<c>403</c>: a wrong client secret, or a service account that lost
    /// <c>manage-users</c>/<c>view-users</c>. Nothing the request can do about it.
    /// </summary>
    NotAuthorized,

    /// <summary>
    /// <c>404</c>: no account for the subject. A caller who just presented a valid token and gets
    /// this had their account deleted mid-session.
    /// </summary>
    AccountNotFound,

    /// <summary>Any other <c>4xx</c>. Permanent — a bug here, not an outage.</summary>
    Rejected,

    /// <summary>Unreachable, timed out, or unreadable. Transient, so a retry is meaningful.</summary>
    Unavailable
}

/// <summary>
/// An exception rather than a result: every caller is a background job, where a throw is the retry
/// signal the outbox and the sweeper are built around, or a handler already committed to the call.
/// </summary>
public sealed class KeycloakAdminException(
    KeycloakAdminFailure reason, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public KeycloakAdminFailure Reason { get; } = reason;
}
