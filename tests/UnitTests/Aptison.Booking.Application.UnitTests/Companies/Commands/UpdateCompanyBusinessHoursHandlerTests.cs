using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class UpdateCompanyBusinessHoursHandlerTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 6);

    private static readonly CompanyBusinessHourAddEditDto TuesdayMorning =
        new() { DayOfWeek = 2, OpenTime = "09:00", CloseTime = "14:30" };

    private static readonly CompanyBusinessHourAddEditDto TuesdayEvening =
        new() { DayOfWeek = 2, OpenTime = "17:00", CloseTime = "21:00" };

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenADayIsSplit_ShouldStoreBothRangesAndReplaceTheOldWeek()
    {
        // Arrange
        var world = new CompanyWorld();
        world.UnitOfWork.Seed(CompanyFactory.CreateBusinessHour(world.CompanyId, DayOfWeek.Monday));

        // Act
        var result = await HandleAsync(world, TuesdayMorning, TuesdayEvening);

        // Assert
        await Assert.That(result.Data!.BusinessHours).IsEquivalentTo(
            [new BusinessHourViewDto(2, "09:00", "14:30"), new BusinessHourViewDto(2, "17:00", "21:00")]);
        await Assert.That(GetLiveHours(world).Select(hour => (hour.DayOfWeek, hour.OpenTime, hour.CloseTime)))
            .IsEquivalentTo([((short)2, new TimeOnly(9, 0), new TimeOnly(14, 30)), ((short)2, new TimeOnly(17, 0), new TimeOnly(21, 0))]);
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenADayHasAFifthRange_ShouldNameItAndStoreNothing()
    {
        // Arrange
        var world = new CompanyWorld();
        var oldHour = CompanyFactory.CreateBusinessHour(world.CompanyId, DayOfWeek.Monday);
        world.UnitOfWork.Seed(oldHour);

        // Act
        var result = await HandleAsync(
            world,
            new CompanyBusinessHourAddEditDto { DayOfWeek = 3, OpenTime = "08:00", CloseTime = "09:00" },
            new CompanyBusinessHourAddEditDto { DayOfWeek = 3, OpenTime = "10:00", CloseTime = "11:00" },
            new CompanyBusinessHourAddEditDto { DayOfWeek = 3, OpenTime = "12:00", CloseTime = "13:00" },
            new CompanyBusinessHourAddEditDto { DayOfWeek = 3, OpenTime = "14:00", CloseTime = "15:00" },
            new CompanyBusinessHourAddEditDto { DayOfWeek = 3, OpenTime = "16:00", CloseTime = "17:00" });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Companies.InvalidBusinessHours);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("BusinessHours[4]");
        await Assert.That(GetLiveHours(world)).IsEquivalentTo([oldHour]);
    }

    [Test]
    [Arguments(0, "09:00", "10:00")]
    [Arguments(8, "09:00", "10:00")]
    [Arguments(2, "10:00", "09:00")]
    [Arguments(2, "09:03", "10:00")]
    [Arguments(2, "09:00", "24:00")]
    public async Task HandleUpdateCompanyBusinessHours_WhenARangeIsMalformed_ShouldNameIt(int day, string open, string close)
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(
            world, TuesdayMorning, new CompanyBusinessHourAddEditDto { DayOfWeek = day, OpenTime = open, CloseTime = close });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Companies.InvalidBusinessHours);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("BusinessHours[1]");
        await Assert.That(GetLiveHours(world)).IsEmpty();
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenTwoRangesOfADayOverlap_ShouldRefuseAsConflictNamingTheLaterOne()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(
            world, TuesdayMorning, new CompanyBusinessHourAddEditDto { DayOfWeek = 2, OpenTime = "14:00", CloseTime = "18:00" });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.BusinessHoursOverlap);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("BusinessHours[1]");
        await Assert.That(GetLiveHours(world)).IsEmpty();
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenTwoRangesTouch_ShouldStoreBoth()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(
            world, TuesdayMorning, new CompanyBusinessHourAddEditDto { DayOfWeek = 2, OpenTime = "14:30", CloseTime = "18:00" });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(GetLiveHours(world).Count).IsEqualTo(2);
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenTheWeekIsEmpty_ShouldCloseTheShopEveryDay()
    {
        // Arrange
        var world = new CompanyWorld();
        world.UnitOfWork.Seed(CompanyFactory.CreateWeeklyBusinessHours(world.CompanyId).ToArray());

        // Act
        var result = await HandleAsync(world);

        // Assert
        await Assert.That(result.Data!.BusinessHours).IsEmpty();
        await Assert.That(GetLiveHours(world)).IsEmpty();
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenABookingFallsInTheRemovedTime_ShouldListItAndMoveNothing()
    {
        // Arrange
        var world = new CompanyWorld();
        var outside = world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(Tuesday, 15, 0));
        world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(Tuesday, 10, 0));

        // Act
        var result = await HandleAsync(world, TuesdayMorning, TuesdayEvening);

        // Assert
        await Assert.That(result.Data!.Conflicts).IsEquivalentTo(
            [new CompanyConflictViewDto(outside.Id, Tuesday, "15:00", "15:30", "Γιώργος Π.", "Nikos B.")]);
        await Assert.That(outside.StartsAt).IsEqualTo(CompanyWorld.AtAthens(Tuesday, 15, 0));
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenAClosureCoversABookedDay_ShouldListTheBookingEvenInsideTheWeek()
    {
        // Arrange
        var world = new CompanyWorld();
        world.SeedHoliday(Tuesday);
        var closed = world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(Tuesday, 10, 0));

        // Act
        var result = await HandleAsync(world, TuesdayMorning);

        // Assert
        await Assert.That(result.Data!.Conflicts.Select(conflict => conflict.AppointmentId)).IsEquivalentTo([closed.Id]);
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenABookingIsPastOrCancelled_ShouldNotListIt()
    {
        // Arrange
        var world = new CompanyWorld();
        world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(CompanyWorld.Today, 8, 0));
        world.SeedAppointment(world.BarberMember, CompanyWorld.AtAthens(Tuesday, 15, 0), status: AppointmentStatus.Cancelled);

        // Act
        var result = await HandleAsync(world, TuesdayMorning);

        // Assert
        await Assert.That(result.Data!.Conflicts).IsEmpty();
    }

    [Test]
    public async Task HandleUpdateCompanyBusinessHours_WhenSaved_ShouldRebuildTheCalendarUnderTheSettingsLock()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        await HandleAsync(world, TuesdayMorning);

        // Assert
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId)]);
        await Assert.That(world.SettingsLock.Acquired).IsEquivalentTo([world.CompanyId]);
    }

    private static List<CompanyBusinessHour> GetLiveHours(CompanyWorld world) =>
        [.. world.UnitOfWork.Entities<CompanyBusinessHour>().Where(hour => !hour.IsDeleted)];

    private static Task<Result<CompanyScheduleChangeResponse, HttpError>> HandleAsync(
        CompanyWorld world, params CompanyBusinessHourAddEditDto[] businessHours) =>
        new UpdateCompanyBusinessHoursHandler(
                world.UnitOfWork, world.Outbox, world.SettingsLock, world.ConstraintReader, world.Clock)
            .ValidateAndHandleAsync(new UpdateCompanyBusinessHoursRequest
            {
                CompanyId = world.CompanyId,
                BusinessHours = businessHours
            });
}
