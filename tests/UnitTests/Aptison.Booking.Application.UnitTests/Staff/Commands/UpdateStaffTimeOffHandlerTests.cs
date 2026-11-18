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

public class UpdateStaffTimeOffHandlerTests
{
    [Test]
    public async Task HandleUpdateStaffTimeOff_WhenAStaffSeniorChangesTheirOwn_ShouldSave()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarberWithTheOwnCode(world);
        var timeOff = SeedWholeDayOff(world, world.BarberMember);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, timeOff.Id, "2026-10-08", "2026-10-09");

        // Assert
        await Assert.That(result.Data!.TimeOff!.StartsOn).IsEqualTo(new DateOnly(2026, 10, 8));
        await Assert.That(result.Data.TimeOff.EndsOn).IsEqualTo(new DateOnly(2026, 10, 9));
    }

    [Test]
    public async Task HandleUpdateStaffTimeOff_WhenAStaffSeniorChangesAColleagues_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarberWithTheOwnCode(world);
        var timeOff = SeedWholeDayOff(world, world.ColleagueMember);

        // Act
        var result = await HandleAsync(world, world.ColleagueMember.Id, timeOff.Id, "2026-10-08", "2026-10-09");

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.NotOwnRow);
    }

    [Test]
    public async Task HandleUpdateStaffTimeOff_WhenAPlatformAdministratorOnAnotherShopChangesTheirOwn_ShouldSave()
    {
        // Arrange
        var world = new StaffWorld();
        var ownMember = world.ActAsPlatformAdministratorFromAnotherShop();
        var timeOff = SeedWholeDayOff(world, ownMember);

        // Act
        var result = await HandleAsync(world, ownMember.Id, timeOff.Id, "2026-10-08", "2026-10-09");

        // Assert
        await Assert.That(result.Data!.TimeOff!.StartsOn).IsEqualTo(new DateOnly(2026, 10, 8));
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>().Single().CompanyId).IsEqualTo(ownMember.OwnerId);
    }

    private static void ActAsBarberWithTheOwnCode(StaffWorld world)
    {
        world.CallerUserId = world.Barber.Id;
        world.Caller.RevokeAll().Grant(PermissionCodes.Members.Read, PermissionCodes.Staff.ManageOwnTimeOff);
    }

    /// <summary>Wednesday 7 October 2026 in Athens.</summary>
    private static StaffTimeOff SeedWholeDayOff(StaffWorld world, StaffMember member)
    {
        var timeOff = StaffFactory.CreateTimeOff(
            member.OwnerId,
            member.Id,
            new DateTime(2026, 10, 6, 21, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 7, 21, 0, 0, DateTimeKind.Utc));

        world.UnitOfWork.Seed(timeOff);
        return timeOff;
    }

    private static Task<Result<StaffScheduleChangeResponse, HttpError>> HandleAsync(
        StaffWorld world, Guid staffMemberId, Guid timeOffId, string startsOn, string endsOn) =>
        new UpdateStaffTimeOffHandler(
                world.UnitOfWork, world.Outbox, world.UserContext, world.Caller, world.ConstraintReader, world.Clock)
            .HandleAsync(
                new UpdateStaffTimeOffRequest
                {
                    CompanyId = world.CompanyId,
                    CallerUserId = world.CallerUserId,
                    StaffMemberId = staffMemberId,
                    TimeOffId = timeOffId,
                    StartsOn = startsOn,
                    EndsOn = endsOn
                },
                CancellationToken.None);
}
