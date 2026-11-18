using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aptison.Booking.Application.UnitTests.Reminders.Commands;

public class SetEmailRemindersByTokenHandlerTests
{
    [Test]
    public async Task HandleSetEmailReminders_WhenAWalkInUnsubscribes_ShouldTurnTheirRemindersOff()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var request = new SetEmailRemindersByTokenRequest { Token = CreateToken(world.WalkIn.Id), Enabled = false };

        // Act
        var result = await CreateHandler(world).HandleAsync(request, CancellationToken.None);

        // Assert
        await Assert.That(result.Data!.EmailRemindersEnabled).IsFalse();
        await Assert.That(result.Data.HasAccount).IsFalse();
        await Assert.That(result.Data.MaskedEmail).IsEqualTo("w•••@example.test");
        await Assert.That(world.UnitOfWork.Entities<User>().Single(user => user.Id == world.WalkIn.Id).EmailRemindersEnabled).IsFalse();
    }

    [Test]
    public async Task HandleSetEmailReminders_WhenUndone_ShouldTurnThemBackOn()
    {
        // Arrange
        var world = ReminderWorld.Create();
        world.Customer.EmailRemindersEnabled = false;
        var request = new SetEmailRemindersByTokenRequest { Token = CreateToken(world.Customer.Id), Enabled = true };

        // Act
        var result = await CreateHandler(world).HandleAsync(request, CancellationToken.None);

        // Assert
        await Assert.That(result.Data!.EmailRemindersEnabled).IsTrue();
        await Assert.That(result.Data.HasAccount).IsTrue();
    }

    [Test]
    public async Task HandleSetEmailReminders_WhenTheTokenIsNotOurs_ShouldChangeNothing()
    {
        // Arrange
        var world = ReminderWorld.Create();
        var request = new SetEmailRemindersByTokenRequest { Token = "forged", Enabled = false };

        // Act
        var result = await CreateHandler(world).HandleAsync(request, CancellationToken.None);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Reminders.InvalidToken);
        await Assert.That(world.UnitOfWork.Entities<User>().All(user => user.EmailRemindersEnabled)).IsTrue();
    }

    private static string CreateToken(Guid userId) =>
        ReminderUnsubscribeToken.Create(userId, TestSecrets.ReminderUnsubscribeSecret);

    private static SetEmailRemindersByTokenHandler CreateHandler(ReminderWorld world) =>
        new(world.UnitOfWork, world.UserContext, ReminderWorld.CreateReminderOptions(), NullLogger<SetEmailRemindersByTokenHandler>.Instance);
}
