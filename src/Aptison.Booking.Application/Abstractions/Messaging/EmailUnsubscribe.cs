namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>
/// The <c>List-Unsubscribe</c> header. <c>OneClick</c> adds <c>List-Unsubscribe-Post</c> (RFC 8058),
/// so <c>Url</c> must then turn reminders off on a POST alone.
/// </summary>
public sealed record EmailUnsubscribe(string Url, bool OneClick);
