namespace Aptison.Booking.Application.Abstractions.Auth.Identity;

/// <summary>
/// The pending email-verification code for a Keycloak subject. Keyed by the subject because an
/// unverified caller has no <c>users</c> row. Implementations store a hash; the plaintext exists
/// once, in <see cref="IssueAsync"/>'s return value.
/// </summary>
public interface IVerificationCodeStore
{
    /// <summary>
    /// Issues a fresh code, replacing any pending one — or refuses, inside the resend cooldown or
    /// past the daily limit. The cooldown check and the write must be one operation, or concurrent
    /// resends send two emails.
    /// </summary>
    Task<VerificationCodeIssue> IssueAsync(Guid subject, CancellationToken cancellationToken);

    /// <summary>
    /// Checks <paramref name="code"/>, consuming an attempt. A correct code is consumed so it
    /// cannot be replayed; reaching the attempt cap destroys the entry.
    /// </summary>
    Task<VerificationCodeCheck> CheckAsync(Guid subject, string code, CancellationToken cancellationToken);
}

/// <summary>What <see cref="IVerificationCodeStore.IssueAsync"/> did.</summary>
public abstract record VerificationCodeIssue
{
    private VerificationCodeIssue() { }

    /// <summary><c>Code</c> is the plaintext, for the email and nothing else.</summary>
    public sealed record Issued(
        string Code,
        DateTimeOffset ExpiresAt,
        TimeSpan ResendAvailableIn,
        int AttemptsAllowed) : VerificationCodeIssue;

    /// <summary>Nothing was issued; the code already sent is still valid.</summary>
    public sealed record ResendTooSoon(TimeSpan RetryAfter) : VerificationCodeIssue;

    /// <summary>
    /// Nothing was issued: the subject had its codes for the day. Each code allows a few guesses,
    /// so this cap is what bounds the guesses a day.
    /// </summary>
    public sealed record DailyLimitReached(TimeSpan RetryAfter) : VerificationCodeIssue;
}

/// <summary>What <see cref="IVerificationCodeStore.CheckAsync"/> found.</summary>
public abstract record VerificationCodeCheck
{
    private VerificationCodeCheck() { }

    /// <summary>Matched and consumed.</summary>
    public sealed record Verified : VerificationCodeCheck;

    /// <summary>Wrong, and the entry survives.</summary>
    public sealed record Incorrect(int AttemptsRemaining) : VerificationCodeCheck;

    /// <summary>The cap was reached and the code destroyed; only a resend helps.</summary>
    public sealed record AttemptsExhausted : VerificationCodeCheck;

    /// <summary>Nothing pending: never issued, or expired, consumed or burned.</summary>
    public sealed record NotPending : VerificationCodeCheck;
}
