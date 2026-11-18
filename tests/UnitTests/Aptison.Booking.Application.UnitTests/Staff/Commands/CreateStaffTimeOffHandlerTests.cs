using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class CreateStaffTimeOffHandlerTests
{
    [Test]
    public async Task HandleCreateStaffTimeOff_WhenWholeDaysCrossTheClockChange_ShouldStoreLocalMidnights()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        // Athens leaves summer time at 04:00 on Sunday 25 October 2026.
        var result = await HandleAsync(world, world.BarberMember.Id, "2026-10-24", "2026-10-26");

        // Assert
        var timeOff = world.UnitOfWork.Entities<StaffTimeOff>().Single();
        await Assert.That(timeOff.StartsAt).IsEqualTo(new DateTime(2026, 10, 23, 21, 0, 0, DateTimeKind.Utc));
        await Assert.That(timeOff.EndsAt).IsEqualTo(new DateTime(2026, 10, 26, 22, 0, 0, DateTimeKind.Utc));
        await Assert.That(result.Data!.TimeOff).IsEqualTo(
            new StaffTimeOffResponse(timeOff.Id, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 26), null, null, "Άδεια"));
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>()).IsEquivalentTo(
            [new CompanyCalendarChanged(world.CompanyId, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 26))]);
    }

    [Test]
    public async Task HandleCreateStaffTimeOff_WhenItOverlapsAnother_ShouldRefuseOnTheStartDate()
    {
        // Arrange
        var world = new StaffWorld();
        world.UnitOfWork.Seed(StaffFactory.CreateTimeOff(
            world.CompanyId,
            world.BarberMember.Id,
            new DateTime(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 22, 21, 0, 0, DateTimeKind.Utc)));

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, "2026-10-22", "2026-10-23");

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.TimeOffOverlap);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("StartsOn");
    }

    [Test]
    public async Task HandleCreateStaffTimeOff_WhenItAbutsAnother_ShouldSave()
    {
        // Arrange
        var world = new StaffWorld();
        world.UnitOfWork.Seed(StaffFactory.CreateTimeOff(
            world.CompanyId,
            world.BarberMember.Id,
            new DateTime(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 21, 21, 0, 0, DateTimeKind.Utc)));

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, "2026-10-22", "2026-10-22");

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task HandleCreateStaffTimeOff_WhenAnUpcomingAppointmentFallsInside_ShouldListIt()
    {
        // Arrange
        var world = new StaffWorld();
        var inside = world.SeedAppointment(world.BarberMember, new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await HandleAsync(
            world, world.BarberMember.Id, "2026-10-07", "2026-10-07", startTime: "10:00", endTime: "12:00");

        // Assert
        await Assert.That(result.Data!.Conflicts.Select(conflict => conflict.AppointmentId)).IsEquivalentTo([inside.Id]);
        await Assert.That(result.Data.TimeOff!.StartTime).IsEqualTo("10:00");
    }

    [Test]
    public async Task HandleCreateStaffTimeOff_WhenOnlyOneTimeIsGiven_ShouldNameTheMissingOne()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, "2026-10-07", "2026-10-07", startTime: "10:00");

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("EndTime");
    }

    [Test]
    public async Task HandleCreateStaffTimeOff_WhenABarberWithTheOwnCodeTargetsAColleague_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        world.CallerUserId = world.Barber.Id;
        world.Caller.RevokeAll().Grant(PermissionCodes.Staff.ManageOwnTimeOff);

        // Act
        var own = await HandleAsync(world, world.BarberMember.Id, "2026-10-07", "2026-10-07");
        var colleagues = await HandleAsync(world, world.ColleagueMember.Id, "2026-10-08", "2026-10-08");

        // Assert
        await Assert.That(own.IsError).IsFalse();
        await Assert.That(colleagues.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.NotOwnRow);
    }

    [Test]
    public async Task HandleCreateStaffTimeOff_WhenAPlatformAdministratorOnAnotherShopAddsTheirOwn_ShouldSaveAtTheirShop()
    {
        // Arrange
        var world = new StaffWorld();
        var ownMember = world.ActAsPlatformAdministratorFromAnotherShop();

        // Act
        var result = await HandleAsync(world, ownMember.Id, "2026-10-07", "2026-10-07");

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<StaffTimeOff>().Single().OwnerId).IsEqualTo(ownMember.OwnerId);
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>()).IsEquivalentTo(
            [new CompanyCalendarChanged(ownMember.OwnerId, new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7))]);
    }

    private static Task<Result<StaffScheduleChangeResponse, HttpError>> HandleAsync(
        StaffWorld world,
        Guid staffMemberId,
        string startsOn,
        string endsOn,
        string? startTime = null,
        string? endTime = null) =>
        new CreateStaffTimeOffHandler(
                world.UnitOfWork, world.Outbox, world.UserContext, world.Caller, world.ConstraintReader, world.Clock)
            .HandleAsync(
                new CreateStaffTimeOffRequest
                {
                    CompanyId = world.CompanyId,
                    CallerUserId = world.CallerUserId,
                    StaffMemberId = staffMemberId,
                    StartsOn = startsOn,
                    EndsOn = endsOn,
                    StartTime = startTime,
                    EndTime = endTime,
                    Reason = "Άδεια"
                },
                CancellationToken.None);
}
