using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class DeleteStaffMemberHandlerTests
{
    [Test]
    public async Task HandleDeleteStaffMember_WhenOnlyACancelledAppointmentNamesThem_ShouldRefuseAsInUse()
    {
        // Arrange
        var world = new StaffWorld();
        world.SeedAppointment(world.BarberMember, StaffWorld.Now.AddDays(3), AppointmentStatus.Cancelled);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.InUse);
        await Assert.That(world.BarberMember.IsDeleted).IsFalse();
    }

    [Test]
    public async Task HandleDeleteStaffMember_WhenNothingNamesThem_ShouldSoftDeleteTheMemberAndEverythingHangingOffIt()
    {
        // Arrange
        var world = new StaffWorld();
        var member = world.BarberMember;
        var service = ServiceFactory.Create(world.CompanyId);
        var assignment = ServiceFactory.CreateStaffService(world.CompanyId, member.Id, service.Id);
        var workHour = StaffFactory.CreateWorkHour(world.CompanyId, member.Id, DayOfWeek.Monday);
        var timeOff = StaffFactory.CreateTimeOff(
            world.CompanyId, member.Id, StaffWorld.Now.AddDays(10), StaffWorld.Now.AddDays(11));
        var grantHere = RoleFactory.CreateGrant(world.Barber.Id, TestIds.Roles.Staff, world.CompanyId);
        var grantElsewhere = RoleFactory.CreateGrant(world.Barber.Id, TestIds.Roles.Staff, Guid.CreateVersion7());
        world.UnitOfWork.Seed(service);
        world.UnitOfWork.Seed(assignment);
        world.UnitOfWork.Seed(workHour);
        world.UnitOfWork.Seed(timeOff);
        world.UnitOfWork.Seed(grantHere, grantElsewhere);

        // Act
        var result = await HandleAsync(world, member.Id);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(member.IsDeleted).IsTrue();
        await Assert.That(assignment.IsDeleted).IsTrue();
        await Assert.That(workHour.IsDeleted).IsTrue();
        await Assert.That(timeOff.IsDeleted).IsTrue();
        await Assert.That(grantHere.IsDeleted).IsTrue();
        await Assert.That(grantElsewhere.IsDeleted).IsFalse();
        await Assert.That(world.CacheInvalidator.InvalidatedUsersIds).IsEquivalentTo([world.Barber.Id]);
    }

    private static Task<Result<HttpError>> HandleAsync(StaffWorld world, Guid staffMemberId) =>
        new DeleteStaffMemberHandler(
                world.UnitOfWork, world.Outbox, world.Caller, world.PermissionResolver, world.CacheInvalidator)
            .HandleAsync(
                new DeleteStaffMemberRequest
                {
                    CompanyId = world.CompanyId, CallerUserId = world.CallerUserId, StaffMemberId = staffMemberId
                },
                CancellationToken.None);
}
