namespace Aptison.Booking.Application.Messaging;

/// <summary>A link in plain text. <c>Url</c> is absolute and not yet encoded.</summary>
public sealed record EmailLink(string Label, string Url);
