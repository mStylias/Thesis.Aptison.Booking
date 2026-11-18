using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class CreateCompanyHolidayHandlerTests
{
    private static readonly DateOnly Saturday = new(2026, 10, 10);
    private static readonly DateOnly ChristmasEve = new(2026, 12, 24);

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenAClosureCoversABookedDay_ShouldStoreItAndListTheBooking()
    {
        // Arrange
        var world = new CompanyWorld();
        var inside = world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(Saturday, 10, 0));
        world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(Saturday.AddDays(1), 10, 0));

        // Act
        var result = await HandleAsync(world, Closure(Saturday, Saturday));

        // Assert
        await Assert.That(result.Data!.Holiday!.StartsOn).IsEqualTo(Saturday);
        await Assert.That(result.Data.Holiday.IsClosed).IsTrue();
        await Assert.That(result.Data.BusinessHours).IsNull();
        await Assert.That(result.Data.Conflicts.Select(conflict => conflict.AppointmentId)).IsEquivalentTo([inside.Id]);
        await Assert.That(world.UnitOfWork.Entities<CompanyHoliday>().Single().OwnerId).IsEqualTo(world.CompanyId);
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenSpecialHoursAreSaved_ShouldListOnlyBookingsOutsideTheWindow()
    {
        // Arrange
        var world = new CompanyWorld();
        world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(ChristmasEve, 10, 0));
        var late = world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(ChristmasEve, 16, 0));

        // Act
        var result = await HandleAsync(world, SpecialHours(ChristmasEve, "09:00", "15:00"));

        // Assert
        await Assert.That(result.Data!.Holiday).IsEqualTo(
            new CompanyHolidayResponse(result.Data.Holiday!.Id, ChristmasEve, ChristmasEve, false, "09:00", "15:00", null));
        await Assert.That(result.Data.Conflicts.Select(conflict => conflict.AppointmentId)).IsEquivalentTo([late.Id]);
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenSaved_ShouldRebuildTheHolidaysDays()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        await HandleAsync(world, Closure(Saturday, Saturday.AddDays(2)));

        // Assert
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, Saturday, Saturday.AddDays(2))]);
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenItSharesADayWithAnother_ShouldRefuseAsConflictOnStartsOn()
    {
        // Arrange
        var world = new CompanyWorld();
        world.SeedHoliday(Saturday, Saturday.AddDays(2));

        // Act
        var result = await HandleAsync(world, Closure(Saturday.AddDays(2), Saturday.AddDays(3)));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.HolidayOverlap);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("StartsOn");
        await Assert.That(world.UnitOfWork.Entities<CompanyHoliday>().Count).IsEqualTo(1);
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenItIs366DaysLong_ShouldStoreIt()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Closure(Saturday, Saturday.AddDays(365)));

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenItIsLongerThan366Days_ShouldNameTheEndDate()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Closure(Saturday, Saturday.AddDays(366)));

        // Assert
        await AssertInvalidAsync(result, "EndsOn");
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenItEndedBeforeToday_ShouldNameTheEndDate()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Closure(CompanyWorld.Today.AddDays(-3), CompanyWorld.Today.AddDays(-1)));

        // Assert
        await AssertInvalidAsync(result, "EndsOn");
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenItEndsToday_ShouldStoreIt()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Closure(CompanyWorld.Today, CompanyWorld.Today));

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenAClosureCarriesTimes_ShouldNameThem()
    {
        // Arrange
        var world = new CompanyWorld();
        var request = Closure(Saturday, Saturday);
        request.OpenTime = "09:00";
        request.CloseTime = "15:00";

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Select(error => error.PropertyName!))
            .IsEquivalentTo(["OpenTime", "CloseTime"]);
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenSpecialHoursHaveNoTimes_ShouldNameBoth()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, SpecialHours(Saturday, null, null));

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Select(error => error.PropertyName!))
            .IsEquivalentTo(["OpenTime", "CloseTime"]);
    }

    [Test]
    [Arguments("09:03", "15:00", "OpenTime")]
    [Arguments("09:00", "15:01", "CloseTime")]
    [Arguments("15:00", "09:00", "CloseTime")]
    public async Task HandleCreateCompanyHoliday_WhenTheWindowIsMalformed_ShouldNameTheField(
        string openTime, string closeTime, string propertyName)
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, SpecialHours(Saturday, openTime, closeTime));

        // Assert
        await AssertInvalidAsync(result, propertyName);
    }

    [Test]
    [Arguments("  Πάσχα  ", "Πάσχα")]
    [Arguments("   ", null)]
    [Arguments(null, null)]
    public async Task HandleCreateCompanyHoliday_WhenANameIsGiven_ShouldStoreItTrimmed(string? name, string? expected)
    {
        // Arrange
        var world = new CompanyWorld();
        var request = Closure(Saturday, Saturday);
        request.Name = name;

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.Data!.Holiday!.Name).IsEqualTo(expected);
        await Assert.That(world.UnitOfWork.Entities<CompanyHoliday>().Single().Name).IsEqualTo(expected);
    }

    [Test]
    public async Task HandleCreateCompanyHoliday_WhenTheNameIsTooLong_ShouldNameIt()
    {
        // Arrange
        var world = new CompanyWorld();
        var request = Closure(Saturday, Saturday);
        request.Name = new string('x', 201);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertInvalidAsync(result, "Name");
    }

    private static async Task AssertInvalidAsync(Result<CompanyScheduleChangeResponse, HttpError> result, string propertyName)
    {
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Companies.InvalidHoliday);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo(propertyName);
    }

    private static CreateCompanyHolidayRequest Closure(DateOnly startsOn, DateOnly endsOn) =>
        new() { StartsOn = startsOn.ToString("yyyy-MM-dd"), EndsOn = endsOn.ToString("yyyy-MM-dd"), IsClosed = true };

    private static CreateCompanyHolidayRequest SpecialHours(DateOnly day, string? openTime, string? closeTime) =>
        new()
        {
            StartsOn = day.ToString("yyyy-MM-dd"),
            EndsOn = day.ToString("yyyy-MM-dd"),
            IsClosed = false,
            OpenTime = openTime,
            CloseTime = closeTime
        };

    private static Task<Result<CompanyScheduleChangeResponse, HttpError>> HandleAsync(
        CompanyWorld world, CreateCompanyHolidayRequest request)
    {
        request.CompanyId = world.CompanyId;

        return new CreateCompanyHolidayHandler(
                world.UnitOfWork, world.Outbox, world.ConstraintReader, FakeWorld.Mapper, world.Clock)
            .ValidateAndHandleAsync(request);
    }
}
