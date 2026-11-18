using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Application.UnitTests.Appointments.Notifications;

/// <summary>The same summary as the confirmation; only the heading says it changed.</summary>
public class AppointmentChangedHandlerTests
{
    [Test]
    [Arguments("el", "Το ραντεβού σου στο Test Barbershop άλλαξε", "Το ραντεβού σου άλλαξε")]
    [Arguments("en", "Your appointment at Test Barbershop has changed", "Your appointment has changed")]
    public async Task HandleAppointmentChanged_WhenComposed_ShouldSayTheAppointmentChanged(
        string languageCode, string subject, string heading)
    {
        // Arrange
        var emails = new FakeEmailSender();

        // Act
        await CreateHandler(emails).HandleAsync(BuildNotification(languageCode), CancellationToken.None);

        // Assert
        var sent = emails.Sent.Single();
        await Assert.That(sent.Subject).IsEqualTo(subject);
        await Assert.That(sent.TextBody).StartsWith(heading);
        await Assert.That(sent.TextBody).Contains("Beard trim");
        await Assert.That(sent.TextBody).Contains(languageCode == "el" ? "11:00 πμ – 11:45 πμ" : "11:00 am – 11:45 am")
            .Because("a changed end or length must show in the mail");
    }

    private static AppointmentChangedHandler CreateHandler(FakeEmailSender emails) =>
        new(emails,
            Options.Create(new EmailBrandingOptions { BrandName = "Aptison", AppUrl = "https://koureia.gr" }),
            Options.Create(new EmailSenderOptions { Address = "no-reply@mail.koureia.gr" }),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero)),
            NullLogger<AppointmentChangedHandler>.Instance);

    private static AppointmentChanged BuildNotification(string languageCode) =>
        new(AppointmentSummaryFactory.Create(
            languageCode,
            new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc),
            durationMinutes: 45,
            services: [new BookedServiceLine("Haircut", 20m, 30), new BookedServiceLine("Beard trim", 10m, 15)],
            totalPrice: 30m,
            calendarSequence: 2,
            staffDisplayName: "Maria K."));
}
