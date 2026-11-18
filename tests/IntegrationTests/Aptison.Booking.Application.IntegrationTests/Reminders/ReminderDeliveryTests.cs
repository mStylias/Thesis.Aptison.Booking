using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Application.Features.Reminders.Notifications;
using Aptison.Booking.Application.Features.Reminders.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Synax.Mediator;

namespace Aptison.Booking.Application.IntegrationTests.Reminders;

/// <summary>
/// What a fake cannot answer: the JSON endpoint lookup in SQL, and reads of a walk-in under
/// <c>BecomeAllOwners</c> from a request with no company.
/// </summary>
public class ReminderDeliveryTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/shared-browser";

    [Test]
    public async Task RegisterPushDevice_WhenASecondUserSignsInOnTheSameBrowser_ShouldMoveTheDevice()
    {
        // Arrange
        var first = UserFactory.Create(keycloakId: Guid.NewGuid());
        var second = UserFactory.Create(keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(first, second);
        ActAsPlatform(first.Id);
        await Sender.SendAsync(BuildRegistration(first.Id));

        // Act
        ActAsPlatform(second.Id);
        var result = await Sender.SendAsync(BuildRegistration(second.Id));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var devices = await DbContext.Set<UserDevice>().AsNoTracking().ToListAsync();
        await Assert.That(devices).Count().IsEqualTo(1);
        await Assert.That(devices[0].UserId).IsEqualTo(second.Id);
    }

    [Test]
    public async Task UnregisterPushDevice_WhenTheCallerOwnsTheBrowser_ShouldRemoveIt()
    {
        // Arrange
        var owner = UserFactory.Create(keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(owner);
        ActAsPlatform(owner.Id);
        await Sender.SendAsync(BuildRegistration(owner.Id));

        // Act
        await Sender.SendAsync(new UnregisterPushDeviceRequest { UserId = owner.Id, Endpoint = Endpoint });

        // Assert
        await Assert.That(await DbContext.Set<UserDevice>().AsNoTracking().AnyAsync()).IsFalse();
    }

    [Test]
    public async Task GetEmailReminderSubscription_WhenTheTokenIsAWalkIns_ShouldFindThemWithNoCompanyInScope()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var walkIn = UserFactory.Create(email: "eleni@example.test", ownerId: company.Id);
        ActAsNobody();
        await SeedAsync(company, walkIn);

        // Act
        var result = await Sender.SendAsync(new GetEmailReminderSubscriptionRequest
        {
            Token = ReminderUnsubscribeToken.Create(walkIn.Id, TestSecrets.ReminderUnsubscribeSecret)
        });

        // Assert
        await Assert.That(result.Data!.MaskedEmail).IsEqualTo("e•••@example.test");
        await Assert.That(result.Data.EmailRemindersEnabled).IsTrue();
    }

    [Test]
    public async Task PublishReminderEmail_WhenTheCustomerIsAWalkIn_ShouldReadThemAndSend()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var walkIn = UserFactory.Create(email: "walk-in@example.test", ownerId: company.Id);
        var barber = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        ActAsNobody();
        await SeedAsync(company, walkIn, barber);
        await SeedAsync(CompanyFactory.CreateTranslation(company.Id, "Barber Studio"));

        var staffMember = StaffFactory.Create(company.Id, barber.Id);
        var service = ServiceFactory.Create(company.Id);
        // The handler sends on the real clock. Whole minutes: Postgres keeps microseconds, and a
        // start that reads back different is a changed time, which drops the reminder.
        var inThreeHours = DateTime.UtcNow.AddHours(3);
        var startsAt = new DateTime(inThreeHours.Ticks - inThreeHours.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var appointment = AppointmentFactory.Create(company.Id, staffMember.Id, walkIn.Id, startsAt);
        ActAsCompany(company.Id);
        await SeedAsync(staffMember, service);
        await SeedAsync(ServiceFactory.CreateTranslation(service.Id, "Κούρεμα"));
        await SeedAsync(appointment);
        await SeedAsync(AppointmentFactory.CreateService(company.Id, appointment.Id, service.Id));

        var reminder = new AppointmentReminder
        {
            Id = Guid.CreateVersion7(),
            AppointmentId = appointment.Id,
            Kind = AppointmentReminderKind.TwoHours,
            StartsAt = appointment.StartsAt,
            Channel = AppointmentReminderChannel.Email,
            Status = AppointmentReminderStatus.Queued,
            DueAt = startsAt.AddHours(-2),
            CreatedAt = startsAt.AddHours(-2)
        };
        ActAsNobody();
        await SeedAsync(reminder);

        // Act
        await Services.GetRequiredService<IPublisher>().PublishAsync(new AppointmentReminderEmailDue(
            reminder.Id, appointment.Id, reminder.Kind, reminder.StartsAt, reminder.DueAt));

        // Assert
        var sent = Services.GetRequiredService<FakeEmailSender>().Sent.Single();
        await Assert.That(sent.To).IsEqualTo("walk-in@example.test");
        await Assert.That(sent.Subject).StartsWith("Σε 2 ώρες:");
        await Assert.That(sent.TextBody).Contains("Κούρεμα");

        var stored = await DbContext.Set<AppointmentReminder>().AsNoTracking().SingleAsync(row => row.Id == reminder.Id);
        await Assert.That(stored.Status).IsEqualTo(AppointmentReminderStatus.Sent);
    }

    private static RegisterPushDeviceRequest BuildRegistration(Guid userId) =>
        new()
        {
            UserId = userId,
            Endpoint = Endpoint,
            Keys = new PushSubscriptionKeysAddDto { P256dh = PushKeys.P256dh, Auth = PushKeys.Auth },
            Label = "Chrome · Windows",
            Platform = UserDevicePlatform.Web
        };
}
