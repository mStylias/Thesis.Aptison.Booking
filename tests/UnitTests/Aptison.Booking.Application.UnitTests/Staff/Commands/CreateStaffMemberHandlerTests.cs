using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class CreateStaffMemberHandlerTests
{
    [Test]
    public async Task HandleCreateStaffMember_WhenTheAddressHasNoLogin_ShouldReturnCandidateNotFound()
    {
        // Arrange
        var world = new StaffWorld();
        world.SeedUser("walk-in@example.test", hasLogin: false);

        // Act
        var result = await HandleAsync(world, "walk-in@example.test");

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.CandidateNotFound);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("Email");
    }

    [Test]
    [Arguments("new-barb")]
    [Arguments("new-barber@example.tes")]
    [Arguments("mew-barber@example.test")]
    public async Task HandleCreateStaffMember_WhenTheAddressIsAPrefixOrOneLetterOff_ShouldReturnCandidateNotFound(
        string email)
    {
        // Arrange
        var world = new StaffWorld();
        world.SeedUser("new-barber@example.test");

        // Act
        var result = await HandleAsync(world, email);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.CandidateNotFound);
    }

    [Test]
    public async Task HandleCreateStaffMember_WhenThePersonIsAlreadyHere_ShouldNameTheirRow()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, world.Barber.Email!);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.AlreadyMember);
        var extensions = ((HttpErrorWithExtensions)result.Error).Extensions;
        await Assert.That(extensions["staffMemberId"]).IsEqualTo(world.BarberMember.Id);
        await Assert.That((bool)extensions["isActive"]!).IsTrue();
    }

    [Test]
    public async Task HandleCreateStaffMember_WhenThePersonIsActiveAtAnotherShop_ShouldRefuseWithoutNamingIt()
    {
        // Arrange
        var world = new StaffWorld();
        var (elsewhere, _) = world.SeedMember("Kostas", "Allou", companyId: Guid.CreateVersion7());

        // Act
        var result = await HandleAsync(world, elsewhere.Email!);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.EmployedElsewhere);
        await Assert.That(world.UnitOfWork.Entities<StaffMember>().Count(member => member.UserId == elsewhere.Id))
            .IsEqualTo(1);
    }

    [Test]
    public async Task HandleCreateStaffMember_WhenRolesAreSentWithoutRolesAssign_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        world.SeedUser("new-barber@example.test");
        world.Caller.RevokeAll().Grant(Domain.Authorization.PermissionCodes.Members.Invite);
        var request = BuildRequest(world, "new-barber@example.test");
        request.RoleIds = [TestIds.Roles.Staff];

        // Act
        var result = await HandleRequestAsync(world, request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(world.UnitOfWork.Entities<StaffMember>()).Count().IsEqualTo(3);
    }

    [Test]
    public async Task HandleCreateStaffMember_WhenTwoRangesOfADayOverlap_ShouldNameTheSecond()
    {
        // Arrange
        var world = new StaffWorld();
        world.SeedUser("new-barber@example.test");
        var request = BuildRequest(world, "new-barber@example.test");
        request.WorkHours =
        [
            new StaffWorkHourAddEditDto { DayOfWeek = 2, StartTime = "09:00", EndTime = "13:00" },
            new StaffWorkHourAddEditDto { DayOfWeek = 2, StartTime = "12:30", EndTime = "18:00" }
        ];

        // Act
        var result = await HandleRequestAsync(world, request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single())
            .IsEqualTo(new ValidationError(ErrorCodes.Staff.WorkHoursOverlap, "Two ranges of one day overlap.", "WorkHours[1]"));
    }

    private static CreateStaffMemberRequest BuildRequest(StaffWorld world, string email) =>
        new()
        {
            CompanyId = world.CompanyId,
            CallerUserId = world.CallerUserId,
            Email = email,
            JobTitleCode = "barber",
            IsBookable = true
        };

    private static Task<Result<StaffMemberResponse, HttpError>> HandleAsync(StaffWorld world, string email) =>
        HandleRequestAsync(world, BuildRequest(world, email));

    private static Task<Result<StaffMemberResponse, HttpError>> HandleRequestAsync(
        StaffWorld world, CreateStaffMemberRequest request) =>
        new CreateStaffMemberHandler(
                world.UnitOfWork,
                world.Outbox,
                world.UserContext,
                world.Caller,
                world.PermissionResolver,
                world.CacheInvalidator,
                world.ConstraintReader,
                world.FileStorageOptions,
                world.Clock)
            .ValidateAndHandleAsync(request);
}
