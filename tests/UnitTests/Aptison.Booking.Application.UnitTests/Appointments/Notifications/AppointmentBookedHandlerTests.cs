using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Application.UnitTests.Appointments.Notifications;

/// <summary>
/// The confirmation mail's copy. Every barbershop is in Greece, so the time is written on a
/// 12-hour clock with a meridiem and the mail never names the timezone.
/// </summary>
public class AppointmentBookedHandlerTests
{
    private static readonly DateTime MorningUtc = new(2026, 9, 23, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfternoonUtc = new(2026, 9, 23, 14, 30, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private const string SenderAddress = "no-reply@mail.koureia.gr";
    private const string AppUrl = "https://koureia.gr";

    [Test]
    public async Task HandleAppointmentBooked_WhenTheCustomerReadsGreek_ShouldWriteAMorningTimeWithPiMi()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", MorningUtc), CancellationToken.None);

        // Assert
        await Assert.That(emails.Sent.Single().TextBody).Contains("Τετάρτη 23 Σεπτεμβρίου, 10:00 πμ");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheAppointmentIsAfterNoon_ShouldWriteItWithMiMi()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", AfternoonUtc), CancellationToken.None);

        // Assert
        await Assert.That(emails.Sent.Single().TextBody).Contains("5:30 μμ");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheCustomerReadsEnglish_ShouldWriteTheMeridiemInLowercase()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("en", AfternoonUtc), CancellationToken.None);

        // Assert
        await Assert.That(emails.Sent.Single().TextBody).Contains("Wednesday 23 September, 5:30 pm");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheCustomerReadsEnglish_ShouldPriceItInEuro()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("en", MorningUtc), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.TextBody).Contains("€20.00");
        await Assert.That(sent.TextBody).DoesNotContain("£");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheMailIsComposed_ShouldNotNameTheTimezone()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", MorningUtc), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.TextBody).DoesNotContain("Athens");
        await Assert.That(sent.HtmlBody).DoesNotContain("Athens");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenSent_ShouldCarryAnInvitationForThatAppointment()
    {
        // Arrange
        var emails = new FakeEmailSender();
        var notification = BuildNotification("el", MorningUtc);

        // Act
        await CreateHandler(emails).HandleAsync(notification, CancellationToken.None);

        // Assert
        var calendar = emails.Sent.Single().Calendar!;
        var content = calendar.Content.Replace("\r\n ", string.Empty);
        await Assert.That(calendar.Method).IsEqualTo("REQUEST");
        await Assert.That(content).Contains($"UID:{notification.Summary.AppointmentId:D}@koureia.gr");
        await Assert.That(content).Contains("SEQUENCE:0");
        await Assert.That(content).Contains("DTSTART;TZID=Europe/Athens:20260923T100000");
        await Assert.That(content).Contains("SUMMARY:Test Barbershop · Haircut");
        await Assert.That(content).Contains($"ORGANIZER;CN=\"Test Barbershop\":mailto:{SenderAddress}")
            .Because("an organizer other than the sender can make Gmail refuse the update");
        await Assert.That(content).Contains("ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:mailto:customer@example.test");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheCustomerHasAnAccount_ShouldLinkToTheAppointmentInIt()
    {
        // Arrange
        var emails = new FakeEmailSender();
        var notification = BuildNotification("el", MorningUtc);
        var url = $"{AppUrl}/el/account/appointments/{notification.Summary.AppointmentId:D}";

        // Act
        await CreateHandler(emails).HandleAsync(notification, CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.HtmlBody).Contains($"href=\"{url}\"");
        await Assert.That(sent.HtmlBody).Contains("Δες το ραντεβού");
        await Assert.That(sent.TextBody).Contains($"Δες το ραντεβού: {url}");
        await Assert.That(sent.Calendar!.Content.Replace("\r\n ", string.Empty)).Contains($"URL:{url}");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheCustomerIsAWalkIn_ShouldLinkToTheShopPage()
    {
        // Arrange
        var emails = new FakeEmailSender();
        var notification = new AppointmentBooked(
            AppointmentSummaryFactory.Create("en", MorningUtc, customerHasAccount: false));

        // Act
        await CreateHandler(emails).HandleAsync(notification, CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.TextBody).Contains($"View barbershop: {AppUrl}/en/barbershops/test-barbershop");
        await Assert.That(sent.HtmlBody).DoesNotContain("/account/");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenComposed_ShouldSayNothingAboutCancelling()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", MorningUtc), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        var description = sent.Calendar!.Content.Replace("\r\n ", string.Empty);
        await Assert.That(sent.TextBody).DoesNotContain("ακυρ");
        await Assert.That(description).DoesNotContain("ακυρ");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheSenderIsNotConfigured_ShouldStillSendTheEmail()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails, senderAddress: string.Empty)
            .HandleAsync(BuildNotification("el", MorningUtc), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.Calendar).IsNull();
        await Assert.That(sent.TextBody).Contains("Τετάρτη 23 Σεπτεμβρίου");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenSent_ShouldOfferTheShopOnAMap()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("en", MorningUtc), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.TextBody).Contains("View on map: https://www.google.com/maps/search/?api=1&query=37.9781,23.7225");
        await Assert.That(sent.HtmlBody).Contains("View on map");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenSent_ShouldOfferACallToTheShop()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", MorningUtc), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.HtmlBody).Contains("href=\"tel:+302101234567\"");
        await Assert.That(sent.HtmlBody).Contains("Κλήση");
        await Assert.That(sent.TextBody).Contains("Τηλέφωνο: +302101234567");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheAppIsConfigured_ShouldPutTheAppointmentBeforeTheMap()
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification("el", MorningUtc), CancellationToken.None);

        // Assert
        var html = emails.Sent.Single().HtmlBody;
        await Assert.That(html).Contains($"{AppUrl}/email/calendar-check.png");
        await Assert.That(html).Contains($"{AppUrl}/email/google-maps.png");
        await Assert.That(html.IndexOf("Δες το ραντεβού", StringComparison.Ordinal))
            .IsLessThan(html.IndexOf("Δες τον χάρτη", StringComparison.Ordinal))
            .Because("the primary button comes first, so a phone stacks it on top");
    }

    [Test]
    public async Task HandleAppointmentBooked_WhenTheCustomerIsAWalkIn_ShouldShowTheStoreIcon()
    {
        // Arrange
        var emails = new FakeEmailSender();
        var notification = new AppointmentBooked(
            AppointmentSummaryFactory.Create("en", MorningUtc, customerHasAccount: false));

        // Act
        await CreateHandler(emails).HandleAsync(notification, CancellationToken.None);

        // Assert
        var html = emails.Sent.Single().HtmlBody;
        await Assert.That(html).Contains($"{AppUrl}/email/store.png");
        await Assert.That(html).DoesNotContain("calendar-check.png");
    }

    private static AppointmentBookedHandler CreateHandler(FakeEmailSender emails, string senderAddress = SenderAddress) =>
        new(emails,
            Options.Create(new EmailBrandingOptions { BrandName = "Aptison", AppUrl = AppUrl }),
            Options.Create(new EmailSenderOptions { Address = senderAddress }),
            new FixedTimeProvider(Now),
            NullLogger<AppointmentBookedHandler>.Instance);

    private static AppointmentBooked BuildNotification(string languageCode, DateTime startsAtUtc) =>
        new(AppointmentSummaryFactory.Create(languageCode, startsAtUtc, companyAddress: "Ermou 1"));
}
