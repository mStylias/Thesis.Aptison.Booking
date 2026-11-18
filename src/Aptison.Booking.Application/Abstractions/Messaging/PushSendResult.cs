namespace Aptison.Booking.Application.Abstractions.Messaging;

public enum PushSendResult
{
    Delivered,

    /// <summary>The push service answered 404 or 410: the browser dropped the subscription.</summary>
    SubscriptionGone
}
