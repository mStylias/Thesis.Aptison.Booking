namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>An iCalendar file sent as an email invitation. <c>Method</c> repeats the file's own METHOD for the MIME header.</summary>
public sealed record EmailCalendarPart(string Method, string Content);
