using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Reminders.Commands;

public class RegisterPushDeviceHandlerTests
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/abc";

    [Test]
    public async Task HandleRegisterPushDevice_WhenTheBrowserIsNew_ShouldStoreItsSubscription()
    {
        // Arrange
        var world = ReminderWorld.Create();

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world.Customer.Id));

        // Assert
        var device = world.UnitOfWork.Entities<UserDevice>().Single();
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.Label).IsEqualTo("Chrome · Android");
        await Assert.That(device.UserId).IsEqualTo(world.Customer.Id);
        await Assert.That(PushDeviceData.ReadSubscription(device.PushData))
            .IsEqualTo(new PushSubscriptionKeys(Endpoint, PushKeys.P256dh, PushKeys.Auth));
    }

    [Test]
    public async Task HandleRegisterPushDevice_WhenAnotherUserRegisteredTheBrowser_ShouldMoveItToTheCaller()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var previous = world.SeedDevice(world.WalkIn.Id, Endpoint);

        // Act
        await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world.Customer.Id));

        // Assert
        var device = world.UnitOfWork.Entities<UserDevice>().Single();
        await Assert.That(device.Id).IsEqualTo(previous.Id);
        await Assert.That(device.UserId).IsEqualTo(world.Customer.Id)
            .Because("a shared computer must never show one person's reminders to the next");
        await Assert.That(device.LastSeenAt).IsEqualTo(ReminderWorld.Now.UtcDateTime);
    }

    [Test]
    public async Task HandleRegisterPushDevice_WhenTheLimitIsReached_ShouldRemoveTheDeviceNotSeenLongest()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var oldest = world.SeedDevice(world.Customer.Id, "https://fcm.googleapis.com/fcm/send/oldest");
        oldest.LastSeenAt = ReminderWorld.Now.UtcDateTime.AddDays(-30);
        world.SeedDevice(world.Customer.Id, "https://fcm.googleapis.com/fcm/send/newer");

        // Act
        await CreateHandler(world, maxDevicesPerUser: 2).ValidateAndHandleAsync(BuildRequest(world.Customer.Id));

        // Assert
        var devices = world.UnitOfWork.Entities<UserDevice>();
        await Assert.That(devices).Count().IsEqualTo(2);
        await Assert.That(devices.Any(device => device.Id == oldest.Id)).IsFalse();
    }

    [Test]
    public async Task HandleRegisterPushDevice_WhenTheKeysAreNotABrowsers_ShouldRefuseThem()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var request = BuildRequest(world.Customer.Id);
        request.Keys.Auth = "too-short";

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Reminders.InvalidSubscription);
    }

    [Test]
    public async Task HandleRegisterPushDevice_WhenTheEndpointIsNotHttps_ShouldRefuseIt()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var request = BuildRequest(world.Customer.Id);
        request.Endpoint = "http://internal.example/push";

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task HandleRegisterPushDevice_WhenTheEnvironmentHasNoVapidKeys_ShouldAnswerUnavailable()
    {
        // Arrange
        var world = ReminderWorld.Create();

        // Act
        var result = await CreateHandler(world, pushEnabled: false).ValidateAndHandleAsync(BuildRequest(world.Customer.Id));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(StatusCodes.Status503ServiceUnavailable);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Reminders.PushNotConfigured);
    }

    private static RegisterPushDeviceRequest BuildRequest(Guid userId) =>
        new()
        {
            UserId = userId,
            Endpoint = Endpoint,
            Keys = new PushSubscriptionKeysAddDto { P256dh = PushKeys.P256dh, Auth = PushKeys.Auth },
            Label = "  Chrome · Android ",
            Platform = UserDevicePlatform.Web
        };

    private static RegisterPushDeviceHandler CreateHandler(ReminderWorld world, int maxDevicesPerUser = 20, bool pushEnabled = true) =>
        new(
            world.UnitOfWork,
            new FakeDatabaseConstraintReader(),
            Options.Create(new PushNotificationOptions { Enabled = pushEnabled, PublicKey = PushKeys.P256dh }),
            Options.Create(new AppointmentReminderOptions
            {
                UnsubscribeSecret = TestSecrets.ReminderUnsubscribeSecret,
                MaxDevicesPerUser = maxDevicesPerUser
            }),
            FakeWorld.Mapper,
            world.Clock,
            NullLogger<RegisterPushDeviceHandler>.Instance);
}
