using System.Text.Json;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Mediator;

namespace Aptison.Booking.Application.IntegrationTests.Reminders;

/// <summary>
/// The scan's SQL: which appointments it picks, and the claim rows and outbox messages it writes.
/// <c>scheduled_at</c> comes from the database clock, so the scan runs two days later than it to
/// count as long after booking.
/// </summary>
public class ScanAppointmentRemindersTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task Scan_WhenTheDayBeforeIsDue_ShouldQueueTheEmailAndSkipPushWithoutDevices()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, scanNow.AddHours(3));

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        var reminders = await ReadRemindersAsync(appointment.Id);
        var email = reminders.Single(reminder => reminder.Channel == AppointmentReminderChannel.Email);
        var push = reminders.Single(reminder => reminder.Channel == AppointmentReminderChannel.Push);
        await Assert.That(email.Kind).IsEqualTo(AppointmentReminderKind.DayBefore);
        await Assert.That(email.Status).IsEqualTo(AppointmentReminderStatus.Queued);
        await Assert.That(email.StartsAt).IsEqualTo(appointment.StartsAt);
        await Assert.That(push.Reason).IsEqualTo(AppointmentReminderReason.NoDevices);
        await Assert.That((await GetOutboxRowsAsync()).Select(row => row.Type)).IsEquivalentTo(["reminders.email-due"]);
    }

    [Test]
    public async Task Scan_WhenRunTwice_ShouldClaimEachReminderOnce()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, scanNow.AddHours(3));

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);
        await CreateScanner(scanNow.AddMinutes(1)).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        await Assert.That(await ReadRemindersAsync(appointment.Id)).Count().IsEqualTo(2);
        await Assert.That(await GetOutboxRowsAsync()).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Scan_WhenTwoHoursIsDueAndTheDayBeforeWasMissed_ShouldSendOnlyTwoHours()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, scanNow.AddHours(1));

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        var reminders = await ReadRemindersAsync(appointment.Id);
        var dayBefore = reminders.Where(reminder => reminder.Kind == AppointmentReminderKind.DayBefore).ToList();
        var twoHoursEmail = reminders.Single(reminder =>
            reminder.Kind == AppointmentReminderKind.TwoHours && reminder.Channel == AppointmentReminderChannel.Email);
        await Assert.That(dayBefore.All(reminder => reminder.Reason == AppointmentReminderReason.Superseded)).IsTrue();
        await Assert.That(twoHoursEmail.Status).IsEqualTo(AppointmentReminderStatus.Queued);
    }

    [Test]
    public async Task Scan_WhenTheCustomerHasTwoDevices_ShouldStageAPushForEach()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, scanNow.AddHours(3));
        ActAsNobody();
        await SeedAsync(CreateDevice(shop.Customer.Id, "one"), CreateDevice(shop.Customer.Id, "two"));

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        var push = (await ReadRemindersAsync(appointment.Id)).Single(reminder => reminder.Channel == AppointmentReminderChannel.Push);
        await Assert.That(push.Status).IsEqualTo(AppointmentReminderStatus.Queued);
        await Assert.That((await GetOutboxRowsAsync()).Count(row => row.Type == "reminders.push-due")).IsEqualTo(2);
    }

    [Test]
    public async Task Scan_WhenAWalkInHasNoEmail_ShouldSkipTheEmail()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var walkIn = UserFactory.Create(ownerId: shop.CompanyId);
        walkIn.Email = null;
        ActAsNobody();
        await SeedAsync(walkIn);
        var appointment = await SeedAppointmentAsync(shop, walkIn.Id, scanNow.AddHours(3));

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        var email = (await ReadRemindersAsync(appointment.Id)).Single(reminder => reminder.Channel == AppointmentReminderChannel.Email);
        await Assert.That(email.Reason).IsEqualTo(AppointmentReminderReason.NoEmail);
        await Assert.That(await GetOutboxRowsAsync()).IsEmpty();
    }

    [Test]
    public async Task Scan_WhenTheTimeChangedAfterTheDayBeforeWasSent_ShouldRemindAgainForTheNewTime()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, scanNow.AddHours(3));
        var oldStartsAt = scanNow.AddHours(-20);
        ActAsNobody();
        await SeedAsync(CreateClaim(appointment.Id, oldStartsAt, AppointmentReminderChannel.Email));

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        var forNewTime = (await ReadRemindersAsync(appointment.Id))
            .Where(reminder => reminder.StartsAt == appointment.StartsAt)
            .ToList();
        await Assert.That(forNewTime).Count().IsEqualTo(2);
        await Assert.That(forNewTime.All(reminder => reminder.Kind == AppointmentReminderKind.DayBefore)).IsTrue();
    }

    [Test]
    public async Task Scan_WhenTheAppointmentWasCancelled_ShouldClaimNothing()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, scanNow.AddHours(3), AppointmentStatus.Cancelled);

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        await Assert.That(await ReadRemindersAsync(appointment.Id)).IsEmpty();
    }

    [Test]
    public async Task Scan_WhenBookedNinetyMinutesBeforeTheStart_ShouldSendNothing()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var bookedAt = scanNow.AddDays(-2);
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, bookedAt.AddMinutes(90));

        // Act
        await CreateScanner(bookedAt.AddMinutes(1)).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        var reminders = await ReadRemindersAsync(appointment.Id);
        await Assert.That(reminders).Count().IsEqualTo(4);
        await Assert.That(reminders.All(reminder => reminder.Reason == AppointmentReminderReason.TooSoonAfterBooking)).IsTrue();
        await Assert.That(await GetOutboxRowsAsync()).IsEmpty();
    }

    [Test]
    public async Task Scan_WhenAnotherInstanceHoldsTheLock_ShouldClaimNothing()
    {
        // Arrange
        var (shop, scanNow) = await SeedShopAsync();
        var appointment = await SeedAppointmentAsync(shop, shop.Customer.Id, scanNow.AddHours(3));
        Services.GetRequiredService<FakeReminderScanLock>().IsHeldElsewhere = true;

        // Act
        await CreateScanner(scanNow).HandleAsync(new ScanAppointmentRemindersRequest(), CancellationToken.None);

        // Assert
        await Assert.That(await ReadRemindersAsync(appointment.Id)).IsEmpty();
    }

    private ScanAppointmentRemindersHandler CreateScanner(DateTime now) =>
        new(
            UnitOfWork,
            UserContext,
            Services.GetRequiredService<FakeReminderScanLock>(),
            Services.GetRequiredService<IOutbox<BookingContext>>(),
            Options.Create(new PushNotificationOptions { Enabled = true }),
            new FixedTimeProvider(new DateTimeOffset(now)),
            NullLogger<ScanAppointmentRemindersHandler>.Instance);

    /// <summary>The shop, and a scan time two days after the database's own clock.</summary>
    private async Task<(ReminderShop Shop, DateTime ScanNow)> SeedShopAsync()
    {
        var databaseNow = await DbContext.Database
            .SqlQuery<DateTime>($"""SELECT statement_timestamp() AS "Value" """)
            .SingleAsync();

        var company = CompanyFactory.Create();
        var customer = UserFactory.Create(keycloakId: Guid.NewGuid());
        var barber = UserFactory.Create();
        ActAsNobody();
        await SeedAsync(company, customer, barber);

        var staffMember = StaffFactory.Create(company.Id, barber.Id);
        ActAsCompany(company.Id);
        await SeedAsync(staffMember);

        return (new ReminderShop(company.Id, staffMember.Id, customer), DateTime.SpecifyKind(databaseNow, DateTimeKind.Utc).AddDays(2));
    }

    private async Task<Appointment> SeedAppointmentAsync(
        ReminderShop shop, Guid customerId, DateTime startsAt, string status = AppointmentStatus.Confirmed)
    {
        var appointment = AppointmentFactory.Create(shop.CompanyId, shop.StaffMemberId, customerId, startsAt, status: status);
        ActAsCompany(shop.CompanyId);
        await SeedAsync(appointment);

        return appointment;
    }

    private Task<List<AppointmentReminder>> ReadRemindersAsync(Guid appointmentId) =>
        DbContext.Set<AppointmentReminder>().AsNoTracking()
            .Where(reminder => reminder.AppointmentId == appointmentId)
            .ToListAsync();

    private static UserDevice CreateDevice(Guid userId, string name) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Platform = UserDevicePlatform.Web,
            PushData = JsonSerializer.SerializeToDocument(new
            {
                endpoint = $"https://fcm.googleapis.com/fcm/send/{name}-{Guid.NewGuid():N}",
                keys = new { p256dh = PushKeys.P256dh, auth = PushKeys.Auth }
            }),
            LastSeenAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

    private static AppointmentReminder CreateClaim(Guid appointmentId, DateTime startsAt, string channel) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AppointmentId = appointmentId,
            Kind = AppointmentReminderKind.DayBefore,
            StartsAt = startsAt,
            Channel = channel,
            Status = AppointmentReminderStatus.Sent,
            DueAt = startsAt.AddHours(-24),
            CreatedAt = startsAt.AddHours(-24)
        };

    private sealed record ReminderShop(Guid CompanyId, Guid StaffMemberId, User Customer);
}
