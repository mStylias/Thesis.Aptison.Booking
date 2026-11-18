namespace Aptison.Booking.Infrastructure.Auth.Identity.Verification;

/// <summary>
/// The verification code's parameters. Part of the wire contract, not private tuning knobs — the
/// send response reports them so the client need not know them.
/// </summary>
public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    /// <summary>
    /// A million possibilities. With <see cref="MaxAttempts"/> and <see cref="MaxCodesPerDay"/> that
    /// is at most 50 guesses a day against it.
    /// </summary>
    public int CodeLength { get; set; } = 6;

    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Reaching it destroys the code.</summary>
    public int MaxAttempts { get; set; } = 5;

    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Codes one subject can be sent in a rolling day, counted from the first.</summary>
    public int MaxCodesPerDay { get; set; } = 10;

    public string KeyPrefix { get; set; } = "auth:verify";
}
