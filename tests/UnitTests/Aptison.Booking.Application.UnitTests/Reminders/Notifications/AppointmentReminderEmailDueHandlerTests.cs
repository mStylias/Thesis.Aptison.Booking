using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.UnitTests.Reminders.Notifications;

/// <summary>
/// The reminder email, built when it is sent. Tomorrow 10:30 in Athens is 07:30 UTC, and the clock
/// sits a minute past the reminder's due time.
/// </summary>
public class AppointmentReminderEmailDueHandlerTests
{
    private static readonly DateTime TomorrowHalfPastTen = new(2026, 10, 21, 7, 30, 0, DateTimeKind.Utc);

    [Test]
    public async Task HandleReminderEmail_WhenTheDayBeforeIsDue_ShouldWriteTheSubjectInGreek()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world);

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        var sent = world.Emails.Sent.Single();
        await Assert.That(sent.Subject).IsEqualTo("Υπενθύμιση: αύριο στις 10:30 πμ στο Barber Studio");
        await Assert.That(sent.TextBody).Contains("Το ραντεβού σου είναι αύριο");
        await Assert.That(sent.To).IsEqualTo("customer@example.test");
    }

    [Test]
    public async Task HandleReminderEmail_WhenTwoHoursIsDueForAnEnglishReader_ShouldSayInTwoHours()
    {
        // Arrange
        var world = ReminderWorld.Create(customerLanguageId: TestIds.Languages.English);
        var appointment = world.SeedAppointment(TomorrowHalfPastTen);
        var reminder = world.SeedReminder(appointment, AppointmentReminderKind.TwoHours, AppointmentReminderChannel.Email);
        world.Clock.Now = new DateTimeOffset(reminder.DueAt.AddMinutes(1));

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        await Assert.That(world.Emails.Sent.Single().Subject).IsEqualTo("In 2 hours: 10:30 am at Barber Studio");
    }

    [Test]
    public async Task HandleReminderEmail_WhenSent_ShouldCarryAOneClickUnsubscribeForTheCustomer()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world);

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        var unsubscribe = world.Emails.Sent.Single().Unsubscribe!;
        var prefix = $"{ReminderWorld.ApiUrl}/api/v1/reminders/unsubscribe/one-click?token=";
        await Assert.That(unsubscribe.OneClick).IsTrue();
        await Assert.That(unsubscribe.Url).StartsWith(prefix);
        await Assert.That(ReminderUnsubscribeToken.Read(
                Uri.UnescapeDataString(unsubscribe.Url[prefix.Length..]), TestSecrets.ReminderUnsubscribeSecret))
            .IsEqualTo(world.Customer.Id);
    }

    [Test]
    public async Task HandleReminderEmail_WhenSent_ShouldLinkTheUnsubscribePageInTheFooter()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world);

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        var sent = world.Emails.Sent.Single();
        await Assert.That(sent.HtmlBody).Contains($"{ReminderWorld.AppUrl}/el/reminders/unsubscribe?token=");
        await Assert.That(sent.TextBody).Contains("Σταμάτησε τις υπενθυμίσεις με email:");
        await Assert.That(sent.TextBody).Contains("Λαμβάνεις αυτό το email επειδή έχεις ραντεβού στο Barber Studio.");
    }

    [Test]
    public async Task HandleReminderEmail_WhenSent_ShouldCarryTheMapButtonAndNoInvitation()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world);

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        var sent = world.Emails.Sent.Single();
        await Assert.That(sent.HtmlBody).Contains("https://www.google.com/maps/search/?api=1&amp;query=37.971578,23.725727");
        await Assert.That(sent.HtmlBody).Contains("Δες τον χάρτη");
        await Assert.That(sent.Calendar).IsNull().Because("the booked and changed emails already put the event in the calendar");
    }

    [Test]
    public async Task HandleReminderEmail_WhenSent_ShouldRecordItAsSent()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world);

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        var stored = world.UnitOfWork.Entities<AppointmentReminder>().Single();
        await Assert.That(stored.Status).IsEqualTo(AppointmentReminderStatus.Sent);
        await Assert.That(stored.CompletedAt).IsEqualTo(world.Clock.Now.UtcDateTime);
    }

    [Test]
    public async Task HandleReminderEmail_WhenTheCustomerIsAWalkIn_ShouldLinkToTheBarbershop()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var appointment = world.SeedAppointment(TomorrowHalfPastTen, customerId: world.WalkIn.Id);
        var reminder = world.SeedReminder(appointment, AppointmentReminderKind.DayBefore, AppointmentReminderChannel.Email);
        world.Clock.Now = new DateTimeOffset(reminder.DueAt.AddMinutes(1));

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        var sent = world.Emails.Sent.Single();
        await Assert.That(sent.To).IsEqualTo("walk-in@example.test");
        await Assert.That(sent.TextBody).Contains($"Δες το κατάστημα: {ReminderWorld.AppUrl}/el/barbershops/");
    }

    [Test]
    public async Task HandleReminderEmail_WhenTheAppointmentWasCancelled_ShouldDropIt()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world, AppointmentStatus.Cancelled);

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        var stored = world.UnitOfWork.Entities<AppointmentReminder>().Single();
        await Assert.That(world.Emails.Sent).IsEmpty();
        await Assert.That(stored.Status).IsEqualTo(AppointmentReminderStatus.Dropped);
        await Assert.That(stored.Reason).IsEqualTo(AppointmentReminderReason.AppointmentInactive);
    }

    [Test]
    public async Task HandleReminderEmail_WhenTheTimeChangedAfterTheClaim_ShouldDropIt()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var appointment = world.SeedAppointment(TomorrowHalfPastTen.AddDays(3));
        var reminder = world.SeedReminder(
            appointment, AppointmentReminderKind.DayBefore, AppointmentReminderChannel.Email, startsAt: TomorrowHalfPastTen);
        world.Clock.Now = new DateTimeOffset(reminder.DueAt.AddMinutes(1));

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        await Assert.That(world.Emails.Sent).IsEmpty();
        await Assert.That(world.UnitOfWork.Entities<AppointmentReminder>().Single().Reason)
            .IsEqualTo(AppointmentReminderReason.TimeChanged);
    }

    [Test]
    public async Task HandleReminderEmail_WhenTheCustomerTurnedEmailRemindersOff_ShouldDropIt()
    {
        // Arrange
        var world = ReminderWorld.Create();
        world.Customer.EmailRemindersEnabled = false;
        var reminder = SeedDayBefore(world);

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        await Assert.That(world.Emails.Sent).IsEmpty();
        await Assert.That(world.UnitOfWork.Entities<AppointmentReminder>().Single().Reason)
            .IsEqualTo(AppointmentReminderReason.EmailRemindersOff);
    }

    [Test]
    public async Task HandleReminderEmail_WhenARetryFindsItAlreadySent_ShouldNotSendAgain()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world);
        reminder.Status = AppointmentReminderStatus.Sent;

        // Act
        await world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        await Assert.That(world.Emails.Sent).IsEmpty();
    }

    [Test]
    public async Task HandleReminderEmail_WhenTheTransportFails_ShouldRecordTheFailureAndRethrow()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var reminder = SeedDayBefore(world);
        world.Emails.ThrowOnSend = new InvalidOperationException("SMTP is down");

        // Act
        var act = () => world.CreateEmailHandler().HandleAsync(ReminderWorld.CreateEmailDue(reminder), CancellationToken.None);

        // Assert
        await Assert.That(act).Throws<InvalidOperationException>().Because("the throw is what makes the outbox retry");
        var stored = world.UnitOfWork.Entities<AppointmentReminder>().Single();
        await Assert.That(stored.Status).IsEqualTo(AppointmentReminderStatus.Failed);
        await Assert.That(stored.Error).Contains("SMTP is down");
    }

    private static AppointmentReminder SeedDayBefore(ReminderWorld world, string status = AppointmentStatus.Confirmed)
    {
        var appointment = world.SeedAppointment(TomorrowHalfPastTen, status: status);
        var reminder = world.SeedReminder(appointment, AppointmentReminderKind.DayBefore, AppointmentReminderChannel.Email);
        world.Clock.Now = new DateTimeOffset(reminder.DueAt.AddMinutes(1));

        return reminder;
    }
}
