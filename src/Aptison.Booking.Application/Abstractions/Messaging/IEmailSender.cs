namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>
/// A transport with no notion of templates or brands: the caller composes, this puts it on the wire.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/>, <b>throwing</b> when the transport refuses it — the throw
    /// is what makes the outbox retry.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// <c>TextBody</c> is not optional: text-only clients and spam filters read that half.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    EmailCalendarPart? Calendar = null,
    EmailUnsubscribe? Unsubscribe = null);
