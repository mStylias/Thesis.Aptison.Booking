namespace Aptison.Booking.Infrastructure.Reminders;

/// <summary>
/// The environment's VAPID key pair, base64url. Generated once and <b>never rotated</b>: every
/// browser subscription is tied to the public key, and new keys make every customer turn push on
/// again. Push stays off until both keys are set.
/// </summary>
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public string PublicKey { get; set; } = string.Empty;

    /// <summary>A secret, one per environment; the committed appsettings leave it empty.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>Who push services contact about this sender: a <c>mailto:</c> or an <c>https:</c> URL (RFC 8292).</summary>
    public string Subject { get; set; } = string.Empty;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey) && !string.IsNullOrWhiteSpace(Subject);
}
