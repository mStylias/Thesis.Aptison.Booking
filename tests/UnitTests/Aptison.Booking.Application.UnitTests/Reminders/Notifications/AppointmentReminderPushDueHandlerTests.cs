using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;

namespace Aptison.Booking.Application.UnitTests.Reminders.Notifications;

/// <summary>The push a device shows, often on a locked phone. Tomorrow 10:30 in Athens is 07:30 UTC.</summary>
public class AppointmentReminderPushDueHandlerTests
{
    private static readonly DateTime TomorrowHalfPastTen = new(2026, 10, 21, 7, 30, 0, DateTimeKind.Utc);

    [Test]
    public async Task HandleReminderPush_WhenTwoHoursIsDue_ShouldPushTheTimeShopServicesAndBarber()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var (reminder, device) = SeedPush(world, AppointmentReminderKind.TwoHours);

        // Act
        await world.CreatePushHandler().HandleAsync(ReminderWorld.CreatePushDue(reminder, device.Id), CancellationToken.None);

        // Assert
        var content = world.Pushes.Sent.Single().Content;
        await Assert.That(content.Title).IsEqualTo("Σε 2 ώρες, στις 10:30 πμ");
        await Assert.That(content.Body).IsEqualTo("Barber Studio · Κούρεμα · Nikos B.");
        await Assert.That(content.Url).IsEqualTo($"/el/account/appointments/{reminder.AppointmentId:D}");
        await Assert.That(content.Tag).IsEqualTo($"appointment-{reminder.AppointmentId:D}");
    }

    [Test]
    public async Task HandleReminderPush_WhenTheDayBeforeIsDue_ShouldSayTomorrow()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var (reminder, device) = SeedPush(world, AppointmentReminderKind.DayBefore);

        // Act
        await world.CreatePushHandler().HandleAsync(ReminderWorld.CreatePushDue(reminder, device.Id), CancellationToken.None);

        // Assert
        var message = world.Pushes.Sent.Single();
        await Assert.That(message.Content.Title).IsEqualTo("Αύριο στις 10:30 πμ");
        await Assert.That(message.Urgency).IsEqualTo(PushUrgency.Normal);
    }

    [Test]
    public async Task HandleReminderPush_WhenSent_ShouldExpireAtTheStartAndReplaceByAppointment()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var (reminder, device) = SeedPush(world, AppointmentReminderKind.TwoHours);

        // Act
        await world.CreatePushHandler().HandleAsync(ReminderWorld.CreatePushDue(reminder, device.Id), CancellationToken.None);

        // Assert
        var message = world.Pushes.Sent.Single();
        await Assert.That(message.TimeToLive).IsEqualTo(TomorrowHalfPastTen - world.Clock.Now.UtcDateTime);
        await Assert.That(message.Urgency).IsEqualTo(PushUrgency.High);
        await Assert.That(message.Topic).IsEqualTo(reminder.AppointmentId.ToString("N"));
        await Assert.That(message.Subscription.Endpoint).IsEqualTo("https://fcm.googleapis.com/fcm/send/device-1");
    }

    [Test]
    public async Task HandleReminderPush_WhenSent_ShouldCarryNoPriceNoteOrAddress()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var (reminder, device) = SeedPush(world, AppointmentReminderKind.TwoHours);

        // Act
        await world.CreatePushHandler().HandleAsync(ReminderWorld.CreatePushDue(reminder, device.Id), CancellationToken.None);

        // Assert
        var content = world.Pushes.Sent.Single().Content;
        var text = content.Title + content.Body;
        await Assert.That(text).DoesNotContain("€");
        await Assert.That(text).DoesNotContain("Short on the sides");
        await Assert.That(text).DoesNotContain("Ermou");
    }

    [Test]
    public async Task HandleReminderPush_WhenThePushServiceForgotTheSubscription_ShouldRemoveTheDevice()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var (reminder, device) = SeedPush(world, AppointmentReminderKind.TwoHours);
        world.Pushes.Result = PushSendResult.SubscriptionGone;

        // Act
        await world.CreatePushHandler().HandleAsync(ReminderWorld.CreatePushDue(reminder, device.Id), CancellationToken.None);

        // Assert
        await Assert.That(world.UnitOfWork.Entities<UserDevice>()).IsEmpty();
        await Assert.That(world.UnitOfWork.Entities<AppointmentReminder>().Single().Reason)
            .IsEqualTo(AppointmentReminderReason.DeviceGone);
    }

    [Test]
    public async Task HandleReminderPush_WhenTheBrowserNowBelongsToAnotherUser_ShouldNotPush()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var (reminder, device) = SeedPush(world, AppointmentReminderKind.TwoHours);
        device.UserId = world.WalkIn.Id;

        // Act
        await world.CreatePushHandler().HandleAsync(ReminderWorld.CreatePushDue(reminder, device.Id), CancellationToken.None);

        // Assert
        await Assert.That(world.Pushes.Sent).IsEmpty()
            .Because("a shared browser must never show one person's reminder to the next");
    }

    [Test]
    public async Task HandleReminderPush_WhenTheAppointmentHasStarted_ShouldDropIt()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var (reminder, device) = SeedPush(world, AppointmentReminderKind.TwoHours);
        world.Clock.Now = new DateTimeOffset(TomorrowHalfPastTen);

        // Act
        await world.CreatePushHandler().HandleAsync(ReminderWorld.CreatePushDue(reminder, device.Id), CancellationToken.None);

        // Assert
        await Assert.That(world.Pushes.Sent).IsEmpty();
        await Assert.That(world.UnitOfWork.Entities<AppointmentReminder>().Single().Reason)
            .IsEqualTo(AppointmentReminderReason.Started);
    }

    private static (AppointmentReminder Reminder, UserDevice Device) SeedPush(ReminderWorld world, string kind)
    {
        var appointment = world.SeedAppointment(TomorrowHalfPastTen);
        var reminder = world.SeedReminder(appointment, kind, AppointmentReminderChannel.Push);
        var device = world.SeedDevice(world.Customer.Id);
        world.Clock.Now = new DateTimeOffset(reminder.DueAt.AddMinutes(1));

        return (reminder, device);
    }
}
