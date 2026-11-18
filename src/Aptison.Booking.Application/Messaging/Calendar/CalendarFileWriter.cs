using System.Globalization;
using System.Text;

namespace Aptison.Booking.Application.Messaging.Calendar;

/// <summary>Writes one event as an iCalendar file (RFC 5545) fit for an email invitation (RFC 6047).</summary>
public static class CalendarFileWriter
{
    private const string ProductId = "-//Koureia//Booking//EN";
    private const int MaxLineOctets = 75;

    public static string GetMethodName(CalendarMethod method) => method == CalendarMethod.Cancel ? "CANCEL" : "REQUEST";

    public static string Write(CalendarEvent calendarEvent, CalendarMethod method, DateTime stampUtc)
    {
        var timeZoneId = GetIanaId(calendarEvent.TimeZone);

        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            $"PRODID:{ProductId}",
            "VERSION:2.0",
            "CALSCALE:GREGORIAN",
            $"METHOD:{GetMethodName(method)}"
        };

        if (timeZoneId is not null)
        {
            lines.AddRange(BuildTimeZone(calendarEvent.TimeZone, timeZoneId, calendarEvent.StartsAtUtc));
        }

        lines.Add("BEGIN:VEVENT");
        lines.Add($"UID:{calendarEvent.Uid}");
        lines.Add(string.Create(CultureInfo.InvariantCulture, $"SEQUENCE:{calendarEvent.Sequence}"));
        lines.Add($"DTSTAMP:{FormatUtc(stampUtc)}");
        lines.Add(FormatInstant("DTSTART", calendarEvent.StartsAtUtc, calendarEvent.TimeZone, timeZoneId));
        lines.Add(FormatInstant("DTEND", calendarEvent.EndsAtUtc, calendarEvent.TimeZone, timeZoneId));
        lines.Add($"SUMMARY:{EscapeText(calendarEvent.Title)}");
        lines.Add($"DESCRIPTION:{EscapeText(calendarEvent.Description)}");
        lines.Add($"LOCATION:{EscapeText(calendarEvent.Location)}");

        if (calendarEvent is { Latitude: { } latitude, Longitude: { } longitude })
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"GEO:{latitude:0.######};{longitude:0.######}"));
            // Apple Calendar draws its map from this, not from GEO.
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"X-APPLE-STRUCTURED-LOCATION;VALUE=URI;X-ADDRESS={QuoteParameter(calendarEvent.Location)};" +
                $"X-APPLE-RADIUS=70;X-TITLE={QuoteParameter(calendarEvent.LocationTitle)}:geo:{latitude:0.######},{longitude:0.######}"));
        }

        lines.Add($"ORGANIZER;CN={QuoteParameter(calendarEvent.OrganizerName)}:mailto:{calendarEvent.OrganizerAddress}");
        lines.Add($"ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:mailto:{calendarEvent.AttendeeAddress}");

        if (calendarEvent.Url is not null)
        {
            lines.Add($"URL:{calendarEvent.Url}");
        }

        lines.Add(method == CalendarMethod.Cancel ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");
        lines.Add("TRANSP:OPAQUE");

        if (method == CalendarMethod.Request)
        {
            foreach (var before in calendarEvent.RemindersBefore)
            {
                lines.Add("BEGIN:VALARM");
                lines.Add("ACTION:DISPLAY");
                lines.Add($"DESCRIPTION:{EscapeText(calendarEvent.Title)}");
                lines.Add($"TRIGGER:-{FormatDuration(before)}");
                lines.Add("END:VALARM");
            }
        }

        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        var file = new StringBuilder();

        foreach (var line in lines)
        {
            file.Append(Fold(line)).Append("\r\n");
        }

        return file.ToString();
    }

    /// <summary>Null writes every time in UTC: a zone with no IANA name cannot be named in TZID.</summary>
    private static string? GetIanaId(TimeZoneInfo timeZone)
    {
        if (timeZone.BaseUtcOffset == TimeSpan.Zero && !timeZone.SupportsDaylightSavingTime)
        {
            return null;
        }

        if (timeZone.HasIanaId)
        {
            return timeZone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId) ? ianaId : null;
    }

    private static string FormatInstant(string name, DateTime utc, TimeZoneInfo timeZone, string? timeZoneId) =>
        timeZoneId is null
            ? $"{name}:{FormatUtc(utc)}"
            : $"{name};TZID={timeZoneId}:{FormatLocal(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), timeZone))}";

    /// <summary>
    /// The real offset changes from a year before the event to a year after, found by probing the zone,
    /// so it matches what .NET converts with on any OS. Outlook needs the block to place a TZID time.
    /// </summary>
    private static IEnumerable<string> BuildTimeZone(TimeZoneInfo timeZone, string timeZoneId, DateTime eventUtc)
    {
        var year = eventUtc.Year;
        var transitions = FindTransitions(
            timeZone,
            new DateTime(year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(year + 2, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        yield return "BEGIN:VTIMEZONE";
        yield return $"TZID:{timeZoneId}";

        if (transitions.Count == 0)
        {
            var offset = timeZone.GetUtcOffset(AsUtc(eventUtc));

            yield return "BEGIN:STANDARD";
            yield return "DTSTART:19700101T000000";
            yield return $"TZOFFSETFROM:{FormatOffset(offset)}";
            yield return $"TZOFFSETTO:{FormatOffset(offset)}";
            yield return "END:STANDARD";
        }

        foreach (var transition in transitions)
        {
            var kind = transition.IsDaylight ? "DAYLIGHT" : "STANDARD";

            yield return $"BEGIN:{kind}";
            // The wall-clock time the change happens at, read on the clock being left.
            yield return $"DTSTART:{FormatLocal(transition.AtUtc + transition.From)}";
            yield return $"TZOFFSETFROM:{FormatOffset(transition.From)}";
            yield return $"TZOFFSETTO:{FormatOffset(transition.To)}";
            yield return $"END:{kind}";
        }

        yield return "END:VTIMEZONE";
    }

    private static List<OffsetTransition> FindTransitions(TimeZoneInfo timeZone, DateTime fromUtc, DateTime untilUtc)
    {
        var transitions = new List<OffsetTransition>();
        var previousOffset = timeZone.GetUtcOffset(fromUtc);

        for (var dayStart = fromUtc; dayStart < untilUtc; dayStart = dayStart.AddDays(1))
        {
            var dayEnd = dayStart.AddDays(1);
            var offset = timeZone.GetUtcOffset(dayEnd);

            if (offset == previousOffset)
            {
                continue;
            }

            // The first minute of the day that reads the new offset.
            int low = 0, high = 24 * 60;

            while (high - low > 1)
            {
                var middle = (low + high) / 2;

                if (timeZone.GetUtcOffset(dayStart.AddMinutes(middle)) == previousOffset)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            var atUtc = dayStart.AddMinutes(high);

            transitions.Add(new OffsetTransition(atUtc, previousOffset, offset, timeZone.IsDaylightSavingTime(atUtc)));
            previousOffset = offset;
        }

        return transitions;
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string FormatUtc(DateTime utc) =>
        AsUtc(utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string FormatLocal(DateTime local) => local.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString("hhmm", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.Ticks % TimeSpan.TicksPerDay == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"P{(int)duration.TotalDays}D");
        }

        return duration.Minutes == 0
            ? string.Create(CultureInfo.InvariantCulture, $"PT{(int)duration.TotalHours}H")
            : string.Create(CultureInfo.InvariantCulture, $"PT{(int)duration.TotalMinutes}M");
    }

    private static string EscapeText(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");

    // A parameter value cannot hold a double quote or a line break, and has no escape for either.
    private static string QuoteParameter(string value) =>
        $"\"{value.Replace('"', '\'').Replace('\r', ' ').Replace('\n', ' ')}\"";

    /// <summary>Lines longer than 75 octets continue after CRLF and a space, never splitting a UTF-8 character.</summary>
    private static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= MaxLineOctets)
        {
            return line;
        }

        var folded = new StringBuilder();
        var lineOctets = 0;

        foreach (var rune in line.EnumerateRunes())
        {
            if (lineOctets + rune.Utf8SequenceLength > MaxLineOctets)
            {
                folded.Append("\r\n ");
                lineOctets = 1;
            }

            folded.Append(rune.ToString());
            lineOctets += rune.Utf8SequenceLength;
        }

        return folded.ToString();
    }

    private sealed record OffsetTransition(DateTime AtUtc, TimeSpan From, TimeSpan To, bool IsDaylight);
}
