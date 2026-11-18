namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>
/// The public half of the environment's VAPID keys, copied from the transport's own settings. Not
/// <see cref="Enabled"/> until both keys and the subject are configured.
/// </summary>
public sealed class PushNotificationOptions
{
    public string PublicKey { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}
