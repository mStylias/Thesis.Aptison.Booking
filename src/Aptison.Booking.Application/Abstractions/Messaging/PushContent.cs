namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>
/// The payload the web app's service worker reads, serialized flat and camel-cased:
/// <c>{ title, body, url, tag }</c>. <c>Url</c> is a path on the web app. Shown on a locked phone.
/// </summary>
public sealed record PushContent(string Title, string Body, string Url, string Tag);
