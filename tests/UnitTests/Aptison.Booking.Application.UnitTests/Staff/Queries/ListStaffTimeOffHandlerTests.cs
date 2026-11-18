using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Queries;

public class ListStaffTimeOffHandlerTests
{
    /// <summary>What the plain <c>staff</c> role holds: no <c>members:read</c>.</summary>
    private static readonly string[] StaffRoleCodes =
    [
        PermissionCodes.Appointments.ReadAll, PermissionCodes.Appointments.Create, PermissionCodes.Appointments.Update,
        PermissionCodes.Appointments.Cancel, PermissionCodes.Services.Read
    ];

    [Test]
    public async Task HandleListStaffTimeOff_WhenAPlainStaffCallerReadsTheirOwn_ShouldListIt()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsPlainBarber(world);
        var timeOff = SeedWholeDayOff(world, world.BarberMember);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id);

        // Assert
        await Assert.That(result.Data!.Select(row => row.Id)).IsEquivalentTo([timeOff.Id]);
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenAPlainStaffCallerReadsAColleagues_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsPlainBarber(world);
        SeedWholeDayOff(world, world.ColleagueMember);

        // Act
        var result = await HandleAsync(world, world.ColleagueMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Authorization.PermissionDenied);
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenAPlainStaffCallerNamesAnUnknownId_ShouldRefuseRatherThanNotFind()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsPlainBarber(world);

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7());

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403).Because("a 404 would tell which ids are members");
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenTheCallersOwnMembershipIsDeactivated_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsPlainBarber(world);
        world.BarberMember.IsActive = false;

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenTheCallerHoldsMembersRead_ShouldListAColleagues()
    {
        // Arrange
        var world = new StaffWorld();
        var timeOff = SeedWholeDayOff(world, world.ColleagueMember);

        // Act
        var result = await HandleAsync(world, world.ColleagueMember.Id);

        // Assert
        await Assert.That(result.Data!.Select(row => row.Id)).IsEquivalentTo([timeOff.Id]);
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenTheCallerHoldsMembersReadAndTheIdIsUnknown_ShouldNotFindIt()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7());

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.NotFound);
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenNoCompanyResolved_ShouldRefuseWithTheScopeCode()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsPlainBarber(world);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, hasCompany: false);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Authorization.CompanyScopeMissing);
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenAPlatformAdministratorOnAnotherShopReadsTheirOwn_ShouldListIt()
    {
        // Arrange
        var world = new StaffWorld();
        var ownMember = world.ActAsPlatformAdministratorFromAnotherShop();
        var timeOff = SeedWholeDayOff(world, ownMember);

        // Act
        var result = await HandleAsync(world, ownMember.Id);

        // Assert
        await Assert.That(result.Data!.Select(row => row.Id)).IsEquivalentTo([timeOff.Id])
            .Because("the own row lives at the membership's shop, not at the shop in the header");
    }

    [Test]
    public async Task HandleListStaffTimeOff_WhenAPlatformAdministratorNamesAnotherShopsMember_ShouldNotFindIt()
    {
        // Arrange
        var world = new StaffWorld();
        world.ActAsPlatformAdministratorFromAnotherShop();
        var (_, strangerMember) = world.SeedMember("Ξένος", "Κουρέας", Guid.CreateVersion7());

        // Act
        var result = await HandleAsync(world, strangerMember.Id);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.NotFound);
    }

    private static void ActAsPlainBarber(StaffWorld world)
    {
        world.CallerUserId = world.Barber.Id;
        world.Caller.RevokeAll().Grant(StaffRoleCodes);
    }

    /// <summary>Wednesday 7 October 2026 in Athens, still ahead of the world's now.</summary>
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

    private static Task<Result<IReadOnlyList<StaffTimeOffResponse>, HttpError>> HandleAsync(
        StaffWorld world, Guid staffMemberId, bool hasCompany = true) =>
        new ListStaffTimeOffHandler(world.UnitOfWork, world.UserContext, world.Caller, world.Clock)
            .ValidateAndHandleAsync(new ListStaffTimeOffRequest
            {
                CompanyId = hasCompany ? world.CompanyId : null,
                CallerUserId = world.CallerUserId,
                StaffMemberId = staffMemberId
            });
}
