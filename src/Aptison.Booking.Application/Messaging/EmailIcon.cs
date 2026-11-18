namespace Aptison.Booking.Application.Messaging;

/// <summary>A PNG the web app serves for emails. <c>Url</c> is absolute and not yet encoded; the size is in CSS pixels.</summary>
public sealed record EmailIcon(string Url, int Width, int Height);
