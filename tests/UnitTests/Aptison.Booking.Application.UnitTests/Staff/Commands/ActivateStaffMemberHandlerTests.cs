using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class ActivateStaffMemberHandlerTests
{
    [Test]
    public async Task HandleActivateStaffMember_WhenTheMemberIsInactive_ShouldActivateAndEvictTheUser()
    {
        // Arrange
        var world = new StaffWorld();
        var (returning, returningMember) = world.SeedMember("Kostas", "Piso", isActive: false);

        // Act
        var result = await HandleAsync(world, returningMember.Id);

        // Assert
        await Assert.That(result.Data!.IsActive).IsTrue();
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
        await Assert.That(world.CacheInvalidator.InvalidatedUsersIds).IsEquivalentTo([returning.Id]);
    }

    [Test]
    public async Task HandleActivateStaffMember_WhenTheInactiveMemberOutranksTheCaller_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        var (formerOwner, formerOwnerMember) = world.SeedMember("Petros", "Palios", isActive: false);
        world.PermissionResolver.Grant(formerOwner.Id, world.CompanyId, StaffWorld.AdministratorCodes);
        world.CallerUserId = world.Barber.Id;
        world.Caller.RevokeAll().Grant(
            [.. StaffWorld.AdministratorCodes.Where(code => code != PermissionCodes.Roles.Assign)]);

        // Act
        var result = await HandleAsync(world, formerOwnerMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.OutranksCaller);
        await Assert.That(formerOwnerMember.IsActive).IsFalse();
        await Assert.That(world.CacheInvalidator.InvalidatedUsersIds).IsEmpty();
    }

    [Test]
    public async Task HandleActivateStaffMember_WhenTheCallerIsTheMember_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, world.OwnerMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.CannotChangeSelf);
    }

    private static Task<Result<StaffMemberResponse, HttpError>> HandleAsync(StaffWorld world, Guid staffMemberId) =>
        new ActivateStaffMemberHandler(
                world.UnitOfWork,
                world.Outbox,
                world.UserContext,
                world.Caller,
                world.PermissionResolver,
                world.CacheInvalidator,
                world.ConstraintReader,
                world.FileStorageOptions,
                world.Clock)
            .HandleAsync(
                new ActivateStaffMemberRequest
                {
                    CompanyId = world.CompanyId, CallerUserId = world.CallerUserId, StaffMemberId = staffMemberId
                },
                CancellationToken.None);
}
