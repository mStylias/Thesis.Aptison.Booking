using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class UpdateCompanyHolidayHandlerTests
{
    private static readonly DateOnly Today = CompanyWorld.Today;

    [Test]
    public async Task HandleUpdateCompanyHoliday_WhenTheHolidayHasEnded_ShouldRefuseAsHistory()
    {
        // Arrange
        var world = new CompanyWorld();
        var ended = world.SeedHoliday(Today.AddDays(-5), Today.AddDays(-1));

        // Act
        var result = await HandleAsync(world, ended.Id, Today.AddDays(-5), Today.AddDays(3));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.HolidayEnded);
        await Assert.That(ended.EndsOn).IsEqualTo(Today.AddDays(-1));
    }

    [Test]
    public async Task HandleUpdateCompanyHoliday_WhenARunningHolidayMovesItsFirstDay_ShouldRefuseOnStartsOn()
    {
        // Arrange
        var world = new CompanyWorld();
        var running = world.SeedHoliday(Today.AddDays(-2), Today.AddDays(2));

        // Act
        var result = await HandleAsync(world, running.Id, Today.AddDays(-1), Today.AddDays(2));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.InvalidHoliday);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("StartsOn");
        await Assert.That(running.StartsOn).IsEqualTo(Today.AddDays(-2));
    }

    [Test]
    public async Task HandleUpdateCompanyHoliday_WhenARunningHolidayIsExtended_ShouldKeepItsFirstDay()
    {
        // Arrange
        var world = new CompanyWorld();
        var running = world.SeedHoliday(Today.AddDays(-2), Today.AddDays(2));

        // Act
        var result = await HandleAsync(world, running.Id, Today.AddDays(-2), Today.AddDays(9));

        // Assert
        await Assert.That(result.Data!.Holiday!.EndsOn).IsEqualTo(Today.AddDays(9));
        await Assert.That(running.StartsOn).IsEqualTo(Today.AddDays(-2));
        await Assert.That(running.EndsOn).IsEqualTo(Today.AddDays(9));
    }

    [Test]
    public async Task HandleUpdateCompanyHoliday_WhenAnUpcomingHolidayMoves_ShouldRebuildTheDaysItLeavesAndCovers()
    {
        // Arrange
        var world = new CompanyWorld();
        var upcoming = world.SeedHoliday(Today.AddDays(10), Today.AddDays(12));

        // Act
        await HandleAsync(world, upcoming.Id, Today.AddDays(5), Today.AddDays(6));

        // Assert
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, Today.AddDays(5), Today.AddDays(12))]);
    }

    [Test]
    public async Task HandleUpdateCompanyHoliday_WhenItWouldShareADayWithAnother_ShouldRefuseAsConflict()
    {
        // Arrange
        var world = new CompanyWorld();
        var upcoming = world.SeedHoliday(Today.AddDays(10), Today.AddDays(12));
        world.SeedHoliday(Today.AddDays(20));

        // Act
        var result = await HandleAsync(world, upcoming.Id, Today.AddDays(10), Today.AddDays(20));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.HolidayOverlap);
        await Assert.That(upcoming.EndsOn).IsEqualTo(Today.AddDays(12));
    }

    [Test]
    public async Task HandleUpdateCompanyHoliday_WhenItOnlyOverlapsItself_ShouldSave()
    {
        // Arrange
        var world = new CompanyWorld();
        var upcoming = world.SeedHoliday(Today.AddDays(10), Today.AddDays(12));

        // Act
        var result = await HandleAsync(world, upcoming.Id, Today.AddDays(11), Today.AddDays(13));

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task HandleUpdateCompanyHoliday_WhenTheHolidayIsUnknown_ShouldReturnNotFound()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7(), Today.AddDays(1), Today.AddDays(1));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.HolidayNotFound);
    }

    private static Task<Result<CompanyScheduleChangeResponse, HttpError>> HandleAsync(
        CompanyWorld world, Guid holidayId, DateOnly startsOn, DateOnly endsOn) =>
        new UpdateCompanyHolidayHandler(world.UnitOfWork, world.Outbox, world.ConstraintReader, FakeWorld.Mapper, world.Clock)
            .ValidateAndHandleAsync(new UpdateCompanyHolidayRequest
            {
                CompanyId = world.CompanyId,
                HolidayId = holidayId,
                StartsOn = startsOn.ToString("yyyy-MM-dd"),
                EndsOn = endsOn.ToString("yyyy-MM-dd"),
                IsClosed = true
            });
}
