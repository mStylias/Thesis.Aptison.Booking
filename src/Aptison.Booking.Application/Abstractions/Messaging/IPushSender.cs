namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>Web Push (RFC 8030, 8291, 8292) straight to the browser's own push service.</summary>
public interface IPushSender
{
    /// <summary>
    /// <b>Throws</b> on a temporary failure — a timeout, 429 or 5xx — which is what makes the outbox
    /// retry. A subscription the push service no longer knows is a result, not a throw.
    /// </summary>
    Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken);
}
