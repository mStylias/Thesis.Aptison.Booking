using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class DeactivateStaffMemberHandlerTests
{
    [Test]
    public async Task HandleDeactivateStaffMember_WhenTheMemberHasAnUpcomingAppointment_ShouldRefuseWithTheCount()
    {
        // Arrange
        var world = new StaffWorld();
        world.SeedAppointment(world.BarberMember, StaffWorld.Now.AddDays(1));
        world.SeedAppointment(world.BarberMember, StaffWorld.Now.AddDays(-1));
        world.SeedAppointment(world.BarberMember, StaffWorld.Now.AddDays(2), AppointmentStatus.Cancelled);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.HasUpcomingAppointments);
        await Assert.That(((HttpErrorWithExtensions)result.Error).Extensions["count"]).IsEqualTo(1);
        await Assert.That(world.BarberMember.IsActive).IsTrue();
    }

    [Test]
    public async Task HandleDeactivateStaffMember_WhenOnlyPastAppointmentsRemain_ShouldDeactivateAndEvictTheUser()
    {
        // Arrange
        var world = new StaffWorld();
        world.SeedAppointment(world.BarberMember, StaffWorld.Now.AddDays(-1));

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id);

        // Assert
        await Assert.That(result.Data!.IsActive).IsFalse();
        await Assert.That(result.Data.BookingBlockers).IsEmpty();
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
        await Assert.That(world.CacheInvalidator.InvalidatedUsersIds).IsEquivalentTo([world.Barber.Id]);
    }

    [Test]
    public async Task HandleDeactivateStaffMember_WhenTheCallerIsTheMember_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, world.OwnerMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.CannotChangeSelf);
        await Assert.That(world.OwnerMember.IsActive).IsTrue();
    }

    [Test]
    public async Task HandleDeactivateStaffMember_WhenAManagerWithoutRolesAssignTargetsTheOwner_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        world.CallerUserId = world.Barber.Id;
        world.Caller.RevokeAll().Grant(
            [.. StaffWorld.AdministratorCodes.Where(code => code != PermissionCodes.Roles.Assign)]);

        // Act
        var result = await HandleAsync(world, world.OwnerMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.OutranksCaller);
    }

    [Test]
    public async Task HandleDeactivateStaffMember_WhenThePlatformAdministratorTargetsTheOwner_ShouldDeactivate()
    {
        // Arrange
        var world = new StaffWorld();
        world.CallerUserId = Guid.CreateVersion7();
        world.Caller.RevokeAll().GrantPlatformAdministrator();

        // Act
        var result = await HandleAsync(world, world.OwnerMember.Id);

        // Assert
        await Assert.That(result.Data!.IsActive).IsFalse();
    }

    private static Task<Result<StaffMemberResponse, HttpError>> HandleAsync(StaffWorld world, Guid staffMemberId) =>
        new DeactivateStaffMemberHandler(
                world.UnitOfWork,
                world.Outbox,
                world.Caller,
                world.PermissionResolver,
                world.CacheInvalidator,
                world.FileStorageOptions,
                world.Clock)
            .HandleAsync(
                new DeactivateStaffMemberRequest
                {
                    CompanyId = world.CompanyId, CallerUserId = world.CallerUserId, StaffMemberId = staffMemberId
                },
                CancellationToken.None);
}
