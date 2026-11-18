namespace Aptison.Booking.Infrastructure.Auth.Identity.Verification;

/// <summary>The sweeper's settings, from <c>EmailVerification:Sweeper</c>.</summary>
public sealed class UnverifiedAccountSweeperOptions
{
    public const string SectionName = "EmailVerification:Sweeper";

    /// <summary>Meaningful only with the realm's <c>verifyEmail</c> at <c>false</c>.</summary>
    public bool Enabled { get; set; }

    /// <summary>On by default, so enabling the sweeper shows its work before destroying anything.</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Generous on purpose: this is the window for finding the mail in a spam folder next morning.
    /// </summary>
    public TimeSpan Cutoff { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Bounds the blast radius of a mistyped cutoff.</summary>
    public int BatchSize { get; set; } = 50;
}
