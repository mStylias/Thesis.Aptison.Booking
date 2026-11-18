using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Application.UnitTests.Appointments.Notifications;

public class AppointmentCancelledHandlerTests
{
    private static readonly DateTime StartsAtUtc = new(2026, 9, 23, 7, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task HandleAppointmentCancelled_WhenAReasonIsIncluded_ShouldStateIt()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", "Ο κουρέας είναι άρρωστος"), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.Subject).IsEqualTo("Το ραντεβού σου στο Test Barbershop ακυρώθηκε");
        await Assert.That(sent.TextBody).Contains("Λόγος: Ο κουρέας είναι άρρωστος");
        await Assert.That(sent.TextBody).Contains("Τετάρτη 23 Σεπτεμβρίου, 10:00 πμ");
    }

    [Test]
    public async Task HandleAppointmentCancelled_WhenNoReasonIsIncluded_ShouldNotMentionOne()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("en", reason: null), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.Subject).IsEqualTo("Your appointment at Test Barbershop was cancelled");
        await Assert.That(sent.TextBody).DoesNotContain("Reason");
        await Assert.That(sent.HtmlBody).DoesNotContain("Reason");
    }

    [Test]
    public async Task HandleAppointmentCancelled_WhenSent_ShouldCancelTheSameEventAtItsNewVersion()
    {
        // Arrange
        var emails = new FakeEmailSender();
        var notification = BuildNotification("el", "Ο κουρέας είναι άρρωστος");

        // Act
        await CreateHandler(emails).HandleAsync(notification, CancellationToken.None);

        // Assert
        var calendar = emails.Sent.Single().Calendar!;
        var content = calendar.Content.Replace("\r\n ", string.Empty);
        await Assert.That(calendar.Method).IsEqualTo("CANCEL");
        await Assert.That(content).Contains($"UID:{notification.Summary.AppointmentId:D}@koureia.gr");
        await Assert.That(content).Contains("SEQUENCE:1");
        await Assert.That(content).Contains("STATUS:CANCELLED");
        await Assert.That(content).Contains("Λόγος: Ο κουρέας είναι άρρωστος");
    }

    [Test]
    public async Task HandleAppointmentCancelled_WhenNoReasonIsIncluded_ShouldLeaveItOutOfTheEvent()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("en", reason: null), CancellationToken.None);

        // Assert
        await Assert.That(emails.Sent.Single().Calendar!.Content).DoesNotContain("Reason");
    }

    [Test]
    public async Task HandleAppointmentCancelled_WhenSent_ShouldStrikeTheTimeAndOfferNoMap()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", "Ο κουρέας είναι άρρωστος"), CancellationToken.None);

        // Assert
        var html = emails.Sent.Single().HtmlBody;
        await Assert.That(html).Contains("line-through");
        await Assert.That(html).Contains("Λόγος");
        await Assert.That(html).Contains("Ο κουρέας είναι άρρωστος");
        await Assert.That(html).DoesNotContain("google.com/maps");
    }

    private static AppointmentCancelledHandler CreateHandler(FakeEmailSender emails) =>
        new(emails,
            Options.Create(new EmailBrandingOptions { BrandName = "Aptison", AppUrl = "https://koureia.gr" }),
            Options.Create(new EmailSenderOptions { Address = "no-reply@mail.koureia.gr" }),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero)),
            NullLogger<AppointmentCancelledHandler>.Instance);

    private static AppointmentCancelled BuildNotification(string languageCode, string? reason) =>
        new(AppointmentSummaryFactory.Create(languageCode, StartsAtUtc, calendarSequence: 1), reason);
}
