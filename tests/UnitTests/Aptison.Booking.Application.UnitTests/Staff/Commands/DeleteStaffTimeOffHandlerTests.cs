using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class DeleteStaffTimeOffHandlerTests
{
    [Test]
    public async Task HandleDeleteStaffTimeOff_WhenAStaffSeniorRemovesTheirOwn_ShouldSoftDeleteIt()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarberWithTheOwnCode(world);
        var timeOff = SeedWholeDayOff(world, world.BarberMember);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, timeOff.Id);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<StaffTimeOff>().Single().IsDeleted).IsTrue();
    }

    [Test]
    public async Task HandleDeleteStaffTimeOff_WhenAStaffSeniorRemovesAColleagues_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarberWithTheOwnCode(world);
        var timeOff = SeedWholeDayOff(world, world.ColleagueMember);

        // Act
        var result = await HandleAsync(world, world.ColleagueMember.Id, timeOff.Id);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.NotOwnRow);
        await Assert.That(world.UnitOfWork.Entities<StaffTimeOff>().Single().IsDeleted).IsFalse();
    }

    [Test]
    public async Task HandleDeleteStaffTimeOff_WhenAPlatformAdministratorOnAnotherShopRemovesTheirOwn_ShouldSoftDeleteIt()
    {
        // Arrange
        var world = new StaffWorld();
        var ownMember = world.ActAsPlatformAdministratorFromAnotherShop();
        var timeOff = SeedWholeDayOff(world, ownMember);

        // Act
        var result = await HandleAsync(world, ownMember.Id, timeOff.Id);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<StaffTimeOff>().Single().IsDeleted).IsTrue();
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

    private static Task<Result<HttpError>> HandleAsync(StaffWorld world, Guid staffMemberId, Guid timeOffId) =>
        new DeleteStaffTimeOffHandler(world.UnitOfWork, world.Outbox, world.UserContext, world.Caller)
            .HandleAsync(
                new DeleteStaffTimeOffRequest
                {
                    CompanyId = world.CompanyId,
                    CallerUserId = world.CallerUserId,
                    StaffMemberId = staffMemberId,
                    TimeOffId = timeOffId
                },
                CancellationToken.None);
}
