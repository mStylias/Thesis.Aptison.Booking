using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class UpdateStaffMemberHandlerTests
{
    private static readonly Guid StaffRoleId = TestIds.Roles.Staff;

    [Test]
    public async Task HandleUpdateStaffMember_WhenTheCallerChangesTheirOwnRoles_ShouldRefuse()
    {
        // Arrange
        var world = CreateWorld();

        // Act
        var result = await HandleAsync(world, world.OwnerMember.Id, roleIds: []);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.CannotChangeSelf);
    }

    [Test]
    public async Task HandleUpdateStaffMember_WhenTheCallerEditsTheirOwnProfile_ShouldSave()
    {
        // Arrange
        var world = CreateWorld();

        // Act
        var result = await HandleAsync(world, world.OwnerMember.Id, roleIds: [StaffRoleId], jobTitleCode: "stylist");

        // Assert
        await Assert.That(result.Data!.JobTitleCode).IsEqualTo("stylist");
        await Assert.That(result.Data.IsSelf).IsTrue();
        await Assert.That(world.CacheInvalidator.InvalidatedUsersIds).IsEmpty().Because("the roles did not change");
    }

    [Test]
    public async Task HandleUpdateStaffMember_WhenRolesChangeWithoutRolesAssign_ShouldRefuse()
    {
        // Arrange
        var world = CreateWorld();
        world.Caller.RevokeAll().Grant(
            [.. StaffWorld.AdministratorCodes.Where(code => code != PermissionCodes.Roles.Assign)]);
        world.PermissionResolver.Grant(world.Barber.Id, world.CompanyId, PermissionCodes.Appointments.ReadAll);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, roleIds: []);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Authorization.PermissionDenied);
    }

    [Test]
    public async Task HandleUpdateStaffMember_WhenARoleIsRemoved_ShouldRevokeItAndEvictTheUser()
    {
        // Arrange
        var world = CreateWorld();

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, roleIds: []);

        // Assert
        await Assert.That(result.Data!.Roles).IsEmpty();
        await Assert.That(world.CacheInvalidator.InvalidatedUsersIds).IsEquivalentTo([world.Barber.Id]);
    }

    [Test]
    public async Task HandleUpdateStaffMember_WhenBookabilityChanges_ShouldStageACatalogChange()
    {
        // Arrange
        var world = CreateWorld();

        // Act
        await HandleAsync(world, world.BarberMember.Id, roleIds: [StaffRoleId], isBookable: false);

        // Assert
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleUpdateStaffMember_WhenTheJobTitleIsUnknown_ShouldNameIt()
    {
        // Arrange
        var world = CreateWorld();

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, roleIds: [StaffRoleId], jobTitleCode: "wizard");

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Staff.InvalidJobTitle);
    }

    /// <summary>Owner and barber both hold the staff role here.</summary>
    private static StaffWorld CreateWorld()
    {
        var world = new StaffWorld();
        world.UnitOfWork.Seed(RoleFactory.Create(StaffRoleId, "staff", isSystem: true));
        world.UnitOfWork.Seed(
            RoleFactory.CreateGrant(world.Owner.Id, StaffRoleId, world.CompanyId),
            RoleFactory.CreateGrant(world.Barber.Id, StaffRoleId, world.CompanyId));
        return world;
    }

    private static Task<Result<StaffMemberResponse, HttpError>> HandleAsync(
        StaffWorld world, Guid staffMemberId, Guid[] roleIds, string jobTitleCode = "barber", bool isBookable = true) =>
        new UpdateStaffMemberHandler(
                world.UnitOfWork,
                world.Outbox,
                world.Caller,
                world.PermissionResolver,
                world.CacheInvalidator,
                world.FileStorageOptions,
                world.Clock)
            .HandleAsync(
                new UpdateStaffMemberRequest
                {
                    CompanyId = world.CompanyId,
                    CallerUserId = world.CallerUserId,
                    StaffMemberId = staffMemberId,
                    JobTitleCode = jobTitleCode,
                    IsBookable = isBookable,
                    JoinedAt = "2024-03-01",
                    RoleIds = roleIds
                },
                CancellationToken.None);
}
