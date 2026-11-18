using Aptison.Booking.Application.Abstractions.Auth.Identity;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// An in-memory stand-in for the Redis-backed store. Issues <see cref="NextCode"/>, counts attempts
/// and burns the entry at the cap, so a handler's branches are all reachable without Redis.
/// </summary>
public sealed class FakeVerificationCodeStore : IVerificationCodeStore
{
    private readonly Dictionary<Guid, Pending> _pending = [];

    public string NextCode { get; set; } = "1234";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromMinutes(1);

    public int AttemptsAllowed { get; set; } = 5;

    /// <summary>Set to refuse the next issue, as the real store does inside the cooldown.</summary>
    public TimeSpan? ResendTooSoonFor { get; set; }

    public List<Guid> IssuedSubjects { get; } = [];

    public Task<VerificationCodeIssue> IssueAsync(Guid subject, CancellationToken cancellationToken)
    {
        if (ResendTooSoonFor is { } retryAfter)
        {
            return Task.FromResult<VerificationCodeIssue>(new VerificationCodeIssue.ResendTooSoon(retryAfter));
        }

        _pending[subject] = new Pending(NextCode, AttemptsAllowed);
        IssuedSubjects.Add(subject);

        return Task.FromResult<VerificationCodeIssue>(new VerificationCodeIssue.Issued(
            NextCode,
            DateTimeOffset.UtcNow + Lifetime,
            ResendCooldown,
            AttemptsAllowed));
    }

    public Task<VerificationCodeCheck> CheckAsync(Guid subject, string code, CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(subject, out var pending))
        {
            return Task.FromResult<VerificationCodeCheck>(new VerificationCodeCheck.NotPending());
        }

        if (pending.Code == code)
        {
            _pending.Remove(subject);
            return Task.FromResult<VerificationCodeCheck>(new VerificationCodeCheck.Verified());
        }

        var remaining = pending.AttemptsRemaining - 1;

        if (remaining <= 0)
        {
            _pending.Remove(subject);
            return Task.FromResult<VerificationCodeCheck>(new VerificationCodeCheck.AttemptsExhausted());
        }

        _pending[subject] = pending with { AttemptsRemaining = remaining };
        return Task.FromResult<VerificationCodeCheck>(new VerificationCodeCheck.Incorrect(remaining));
    }

    private sealed record Pending(string Code, int AttemptsRemaining);
}
