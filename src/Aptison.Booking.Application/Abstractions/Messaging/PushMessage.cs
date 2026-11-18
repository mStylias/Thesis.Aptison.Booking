namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>
/// <c>Topic</c> replaces an undelivered push with the same topic on a device that was offline: at
/// most 32 characters of the URL-safe base64 alphabet.
/// </summary>
public sealed record PushMessage(
    PushSubscriptionKeys Subscription,
    PushContent Content,
    TimeSpan TimeToLive,
    PushUrgency Urgency,
    string? Topic);
