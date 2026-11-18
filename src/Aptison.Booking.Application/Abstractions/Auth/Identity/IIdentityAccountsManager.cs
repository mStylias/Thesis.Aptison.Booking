namespace Aptison.Booking.Application.Abstractions.Auth.Identity;

/// <summary>
/// What this application asks of the identity provider's account store. <b>Creating accounts is
/// deliberately absent</b>: registration stays with Keycloak so the password never reaches this
/// codebase.
/// </summary>
public interface IIdentityAccountsManager
{
    /// <summary>
    /// Marks the address verified. Does <b>not</b> change an already-issued token — the caller's
    /// access token keeps saying <c>email_verified: false</c> until it is exchanged.
    /// </summary>
    Task MarkEmailVerifiedAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>One account, or <c>null</c> when no such subject exists.</summary>
    Task<IdentityAccount?> GetAccountAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the name and language into the provider: the name reaches the next token and its own
    /// pages, the language the sign-in and password-reset pages. This database stays the source of
    /// truth; losing the copy costs a stale login form, not a wrong profile.
    /// </summary>
    Task SetProfileAsync(
        Guid subject, string firstName, string lastName, string languageCode, CancellationToken cancellationToken);

    /// <summary>Deletes an account. Used only by the sweeper.</summary>
    Task DeleteAccountAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>The identity providers linked to the account, such as <c>google</c>. Empty when none is.</summary>
    Task<IReadOnlyList<string>> ListLinkedProvidersAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>Whether the account can sign in with a password.</summary>
    Task<bool> HasPasswordAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every password on the account. A credential another caller removed first counts as
    /// removed, so two concurrent calls both succeed.
    /// </summary>
    Task RemovePasswordsAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session, so no refresh token of the account works. Access tokens already issued
    /// stay valid at the provider until they expire.
    /// </summary>
    Task EndSessionsAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>
    /// Unverified accounts created before <paramref name="createdBefore"/> — the sweeper's work
    /// list, in batches of <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<IdentityAccount>> ListUnverifiedAsync(
        DateTimeOffset createdBefore, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// An account as the provider holds it. <c>Subject</c> is the Keycloak <c>sub</c>, stored here as
/// <c>users.keycloak_id</c>; <c>PreferredLanguageCode</c> is null until something writes it, which
/// is the ordinary state of a locally registered or brokered account.
/// </summary>
public sealed record IdentityAccount(
    Guid Subject,
    string? Email,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    string? PreferredLanguageCode);
