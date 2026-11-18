using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class DeleteCompanyHolidayHandlerTests
{
    private static readonly DateOnly Today = CompanyWorld.Today;

    [Test]
    public async Task HandleDeleteCompanyHoliday_WhenTheHolidayHasEnded_ShouldRefuseAsHistory()
    {
        // Arrange
        var world = new CompanyWorld();
        var ended = world.SeedHoliday(Today.AddDays(-5), Today.AddDays(-1));

        // Act
        var result = await HandleAsync(world, ended.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.HolidayEnded);
        await Assert.That(ended.IsDeleted).IsFalse();
    }

    [Test]
    public async Task HandleDeleteCompanyHoliday_WhenTheHolidayIsRunning_ShouldEndItYesterdayAndKeepTheRow()
    {
        // Arrange
        var world = new CompanyWorld();
        var running = world.SeedHoliday(Today.AddDays(-2), Today.AddDays(2));

        // Act
        var result = await HandleAsync(world, running.Id);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(running.IsDeleted).IsFalse();
        await Assert.That(running.StartsOn).IsEqualTo(Today.AddDays(-2));
        await Assert.That(running.EndsOn).IsEqualTo(Today.AddDays(-1));
    }

    [Test]
    public async Task HandleDeleteCompanyHoliday_WhenTheHolidayIsUpcoming_ShouldSoftDeleteItAndRebuildItsDays()
    {
        // Arrange
        var world = new CompanyWorld();
        var upcoming = world.SeedHoliday(Today.AddDays(10), Today.AddDays(12));

        // Act
        var result = await HandleAsync(world, upcoming.Id);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(upcoming.IsDeleted).IsTrue();
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, Today.AddDays(10), Today.AddDays(12))]);
    }

    [Test]
    public async Task HandleDeleteCompanyHoliday_WhenTheHolidayStartsToday_ShouldSoftDeleteIt()
    {
        // Arrange
        var world = new CompanyWorld();
        var startingToday = world.SeedHoliday(Today, Today.AddDays(1));

        // Act
        await HandleAsync(world, startingToday.Id);

        // Assert
        await Assert.That(startingToday.IsDeleted).IsTrue();
    }

    [Test]
    public async Task HandleDeleteCompanyHoliday_WhenTheHolidayIsUnknown_ShouldReturnNotFound()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7());

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.HolidayNotFound);
    }

    private static Task<Result<HttpError>> HandleAsync(CompanyWorld world, Guid holidayId) =>
        new DeleteCompanyHolidayHandler(world.UnitOfWork, world.Outbox, world.Clock)
            .HandleAsync(new DeleteCompanyHolidayRequest { CompanyId = world.CompanyId, HolidayId = holidayId }, default);
}
