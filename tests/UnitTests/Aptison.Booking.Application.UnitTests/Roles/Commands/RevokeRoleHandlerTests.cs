using Aptison.Booking.Application.Features.Roles.Commands;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Testing;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Roles.Commands;

public class RevokeRoleHandlerTests
{
    private static readonly string[] OwnerCodes =
    [
        PermissionCodes.Roles.Assign, PermissionCodes.Roles.Create, PermissionCodes.Members.Remove,
        PermissionCodes.Appointments.ReadAll
    ];

    private static readonly string[] ManagerCodes = [PermissionCodes.Roles.Assign, PermissionCodes.Appointments.ReadAll];

    private readonly Guid _companyId = Guid.CreateVersion7();
    private readonly Guid _ownerId = Guid.CreateVersion7();
    private readonly Guid _managerId = Guid.CreateVersion7();
    private readonly Guid _barberId = Guid.CreateVersion7();
    private readonly FakeUnitOfWork<BookingContext> _unitOfWork;
    private readonly FakePermissionResolver _permissionResolver = new();
    private readonly FakePermissionCacheInvalidator _cacheInvalidator = new();

    public RevokeRoleHandlerTests()
    {
        _unitOfWork = FakeWorld.CreateUnitOfWork(company: _companyId, userContext: FakeWorld.CreateUserContext());
        _permissionResolver
            .Grant(_ownerId, _companyId, OwnerCodes)
            .Grant(_managerId, _companyId, ManagerCodes)
            .Grant(_barberId, _companyId, PermissionCodes.Appointments.ReadAll);
    }

    [Test]
    public async Task HandleRevokeRole_WhenTheCallerHoldsEverythingTheUserHolds_ShouldRemoveTheGrantAndEvictTheUser()
    {
        // Arrange
        var grant = SeedGrant(_barberId, TestIds.Roles.Staff);
        var caller = new FakeCurrentPermissions().Grant(ManagerCodes);

        // Act
        var result = await HandleAsync(caller, _managerId, _barberId, TestIds.Roles.Staff);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(grant.IsDeleted).IsTrue();
        await Assert.That(_cacheInvalidator.InvalidatedUsersIds).IsEquivalentTo([_barberId]);
    }

    [Test]
    public async Task HandleRevokeRole_WhenTheUserOutranksTheCaller_ShouldRefuse()
    {
        // Arrange
        var grant = SeedGrant(_ownerId, TestIds.Roles.CompanyAdmin);
        var caller = new FakeCurrentPermissions().Grant(ManagerCodes);

        // Act
        var result = await HandleAsync(caller, _managerId, _ownerId, TestIds.Roles.CompanyAdmin);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.OutranksCaller);
        await Assert.That(grant.IsDeleted).IsFalse();
        await Assert.That(_cacheInvalidator.InvalidatedUsersIds).IsEmpty();
    }

    [Test]
    public async Task HandleRevokeRole_WhenTheCallerRevokesTheirOwnGrant_ShouldRefuse()
    {
        // Arrange
        var grant = SeedGrant(_ownerId, TestIds.Roles.CompanyAdmin);
        var caller = new FakeCurrentPermissions().Grant(OwnerCodes);

        // Act
        var result = await HandleAsync(caller, _ownerId, _ownerId, TestIds.Roles.CompanyAdmin);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.CannotChangeSelf);
        await Assert.That(grant.IsDeleted).IsFalse();
    }

    [Test]
    public async Task HandleRevokeRole_WhenThePlatformAdministratorTargetsTheOwner_ShouldRemoveTheGrant()
    {
        // Arrange
        var grant = SeedGrant(_ownerId, TestIds.Roles.CompanyAdmin);
        var caller = new FakeCurrentPermissions().GrantPlatformAdministrator();

        // Act
        var result = await HandleAsync(caller, Guid.CreateVersion7(), _ownerId, TestIds.Roles.CompanyAdmin);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(grant.IsDeleted).IsTrue();
    }

    private UserRole SeedGrant(Guid userId, Guid roleId)
    {
        var grant = RoleFactory.CreateGrant(userId, roleId, _companyId);
        _unitOfWork.Seed(grant);
        return grant;
    }

    private Task<Result<HttpError>> HandleAsync(
        FakeCurrentPermissions caller, Guid callerUserId, Guid userId, Guid roleId) =>
        new RevokeRoleHandler(_unitOfWork, caller, _permissionResolver, _cacheInvalidator)
            .HandleAsync(
                new RevokeRoleRequest
                {
                    CompanyId = _companyId, CallerUserId = callerUserId, UserId = userId, RoleId = roleId
                },
                CancellationToken.None);
}
