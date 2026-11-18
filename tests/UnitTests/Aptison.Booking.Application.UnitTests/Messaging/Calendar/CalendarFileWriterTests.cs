using System.Text;
using Aptison.Booking.Application.Messaging.Calendar;

namespace Aptison.Booking.Application.UnitTests.Messaging.Calendar;

public class CalendarFileWriterTests
{
    private static readonly DateTime StampUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo Athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");

    [Test]
    public async Task Write_WhenTheAppointmentIsOnTheNightTheClocksChange_ShouldKeepTheShopLocalTime()
    {
        // Arrange
        // 25 October 2026, 10:00 in Athens: after the 04:00 change, so UTC+2.
        var calendarEvent = BuildEvent(startsAtUtc: new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc));

        // Act
        var file = CalendarFileWriter.Write(calendarEvent, CalendarMethod.Request, StampUtc);

        // Assert
        var lines = Unfold(file);
        await Assert.That(lines).Contains("DTSTART;TZID=Europe/Athens:20261025T100000");
        await Assert.That(lines).Contains("DTEND;TZID=Europe/Athens:20261025T103000");
        await Assert.That(file).Contains("BEGIN:STANDARD\r\nDTSTART:20261025T040000\r\nTZOFFSETFROM:+0300\r\nTZOFFSETTO:+0200\r\nEND:STANDARD")
            .Because("Outlook places a TZID time only through the VTIMEZONE block");
        await Assert.That(file).Contains("BEGIN:DAYLIGHT\r\nDTSTART:20260329T030000\r\nTZOFFSETFROM:+0200\r\nTZOFFSETTO:+0300\r\nEND:DAYLIGHT");
    }

    [Test]
    public async Task Write_WhenALineIsLongerThan75Octets_ShouldFoldItWithoutSplittingACharacter()
    {
        // Arrange
        var calendarEvent = BuildEvent(description: string.Concat(Enumerable.Repeat("Κούρεμα και ξύρισμα με ζεστή πετσέτα. ", 6)));

        // Act
        var file = CalendarFileWriter.Write(calendarEvent, CalendarMethod.Request, StampUtc);

        // Assert
        var physicalLines = file.Split("\r\n");
        await Assert.That(physicalLines.Max(line => Encoding.UTF8.GetByteCount(line))).IsLessThanOrEqualTo(75);
        await Assert.That(file).DoesNotContain("�");
        await Assert.That(Unfold(file)).Contains($"DESCRIPTION:{calendarEvent.Description}");
    }

    [Test]
    public async Task Write_WhenTheTextHoldsSeparators_ShouldEscapeThem()
    {
        // Arrange
        var calendarEvent = BuildEvent(title: "Barber X · Κούρεμα, Γένια", description: "Line one; still one\nLine two");

        // Act
        var file = CalendarFileWriter.Write(calendarEvent, CalendarMethod.Request, StampUtc);

        // Assert
        var lines = Unfold(file);
        await Assert.That(lines).Contains("SUMMARY:Barber X · Κούρεμα\\, Γένια");
        await Assert.That(lines).Contains("DESCRIPTION:Line one\\; still one\\nLine two");
    }

    [Test]
    public async Task Write_WhenRequesting_ShouldCarryTheIdentityTheVersionAndBothReminders()
    {
        // Arrange
        var calendarEvent = BuildEvent(sequence: 3);

        // Act
        var file = CalendarFileWriter.Write(calendarEvent, CalendarMethod.Request, StampUtc);

        // Assert
        var lines = Unfold(file);
        await Assert.That(lines).Contains("METHOD:REQUEST");
        await Assert.That(lines).Contains("UID:appointment@koureia.gr");
        await Assert.That(lines).Contains("SEQUENCE:3");
        await Assert.That(lines).Contains("STATUS:CONFIRMED");
        await Assert.That(lines).Contains("TRANSP:OPAQUE");
        await Assert.That(lines).Contains("ORGANIZER;CN=\"Barber X\":mailto:no-reply@mail.koureia.gr");
        await Assert.That(lines).Contains("ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:mailto:customer@example.test");
        await Assert.That(lines).Contains("GEO:37.9781;23.7225");
        await Assert.That(lines).Contains("TRIGGER:-P1D");
        await Assert.That(lines).Contains("TRIGGER:-PT2H");
    }

    [Test]
    public async Task Write_WhenCancelling_ShouldMarkItCancelledWithNoReminders()
    {
        // Arrange
        var calendarEvent = BuildEvent(sequence: 4);

        // Act
        var file = CalendarFileWriter.Write(calendarEvent, CalendarMethod.Cancel, StampUtc);

        // Assert
        var lines = Unfold(file);
        await Assert.That(lines).Contains("METHOD:CANCEL");
        await Assert.That(lines).Contains("STATUS:CANCELLED");
        await Assert.That(lines).Contains("SEQUENCE:4");
        await Assert.That(file).DoesNotContain("BEGIN:VALARM");
    }

    [Test]
    public async Task Write_WhenTheZoneIsUtc_ShouldWriteUtcTimesAndNoTimeZoneBlock()
    {
        // Arrange
        var calendarEvent = BuildEvent(timeZone: TimeZoneInfo.Utc);

        // Act
        var file = CalendarFileWriter.Write(calendarEvent, CalendarMethod.Request, StampUtc);

        // Assert
        await Assert.That(Unfold(file)).Contains("DTSTART:20261025T080000Z");
        await Assert.That(file).DoesNotContain("BEGIN:VTIMEZONE");
    }

    private static CalendarEvent BuildEvent(
        DateTime? startsAtUtc = null,
        string title = "Barber X · Κούρεμα",
        string description = "Κουρέας: Nikos B.",
        int sequence = 0,
        TimeZoneInfo? timeZone = null)
    {
        var startsAt = startsAtUtc ?? new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc);

        return new CalendarEvent(
            Uid: "appointment@koureia.gr",
            Sequence: sequence,
            StartsAtUtc: startsAt,
            EndsAtUtc: startsAt.AddMinutes(30),
            TimeZone: timeZone ?? Athens,
            Title: title,
            Description: description,
            Location: "Ermou 1, Athens",
            LocationTitle: "Barber X",
            Latitude: 37.9781,
            Longitude: 23.7225,
            OrganizerName: "Barber X",
            OrganizerAddress: "no-reply@mail.koureia.gr",
            AttendeeAddress: "customer@example.test",
            Url: null,
            RemindersBefore: [TimeSpan.FromDays(1), TimeSpan.FromHours(2)]);
    }

    private static List<string> Unfold(string file) => [.. file.Replace("\r\n ", string.Empty).Split("\r\n")];
}
