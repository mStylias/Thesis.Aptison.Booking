namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// The current request's <c>users.id</c>, already translated from the Keycloak subject. The two id
/// spaces are separate: <c>User.KeycloakId</c> is nullable (walk-in customers have no account),
/// while the audit columns are FKs to <c>users.id</c>, so stamping a raw subject violates the FK.
/// Resolved once per request by <c>CurrentAppUserMiddleware</c>, so the providers reading it do no
/// I/O.
/// </summary>
public interface ICurrentAppUser
{
    /// <summary>
    /// The current user's <c>users.id</c>, or <c>null</c> when the request is anonymous or
    /// unverified — both real states, and both must still be able to save.
    /// <para>
    /// The invariant is <em>authenticated and verified ⇒ has a row</em>. Do not tighten this to a
    /// non-nullable <c>Guid</c> or provision earlier to remove the null:
    /// <c>EmailVerifiedRequirement</c> is what keeps an unverified request away from the handlers
    /// that read it.
    /// </para>
    /// </summary>
    Guid? UserId { get; }

    /// <exception cref="InvalidOperationException">The request carries no authenticated user.</exception>
    Guid GetRequiredUserId();
}
