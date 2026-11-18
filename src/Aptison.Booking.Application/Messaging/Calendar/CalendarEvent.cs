namespace Aptison.Booking.Application.Messaging.Calendar;

/// <summary>One event as <see cref="CalendarFileWriter"/> writes it. Every string is plain text; the writer escapes it.</summary>
public sealed record CalendarEvent(
    string Uid,
    int Sequence,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    TimeZoneInfo TimeZone,
    string Title,
    string Description,
    string Location,
    string LocationTitle,
    double? Latitude,
    double? Longitude,
    string OrganizerName,
    string OrganizerAddress,
    string AttendeeAddress,
    string? Url,
    IReadOnlyList<TimeSpan> RemindersBefore);
