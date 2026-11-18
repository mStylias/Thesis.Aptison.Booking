namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>What a browser's <c>PushSubscription</c> hands over: where to post, and the two encryption keys.</summary>
public sealed record PushSubscriptionKeys(string Endpoint, string P256dh, string Auth);
