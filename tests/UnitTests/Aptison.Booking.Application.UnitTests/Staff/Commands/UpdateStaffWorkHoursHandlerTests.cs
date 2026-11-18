using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class UpdateStaffWorkHoursHandlerTests
{
    private static readonly StaffWorkHourAddEditDto MondayMorning =
        new() { DayOfWeek = 1, StartTime = "09:00", EndTime = "13:00" };

    /// <summary>The seeded <c>staff</c> role: neither schedule code.</summary>
    private static readonly string[] StaffRoleCodes =
    [
        PermissionCodes.Appointments.ReadAll, PermissionCodes.Appointments.Create, PermissionCodes.Appointments.Update,
        PermissionCodes.Appointments.Cancel, PermissionCodes.Services.Read
    ];

    /// <summary>The seeded <c>staff_senior</c> role, less its service and customer codes.</summary>
    private static readonly string[] StaffSeniorRoleCodes =
    [
        .. StaffRoleCodes, PermissionCodes.Members.Read, PermissionCodes.Staff.ManageOwnSchedule,
        PermissionCodes.Staff.ManageOwnTimeOff
    ];

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenABarberWithTheOwnCodeWritesTheirOwnHours_ShouldSave()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarber(world, PermissionCodes.Staff.ManageOwnSchedule);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.Data!.WorkHours).IsEquivalentTo([new StaffWorkHourViewDto(1, "09:00", "13:00")]);
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenABarberWithTheOwnCodeWritesAColleaguesHours_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarber(world, PermissionCodes.Staff.ManageOwnSchedule);

        // Act
        var result = await HandleAsync(world, world.ColleagueMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.NotOwnRow);
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenABarberHoldsNeitherCode_ShouldRefuseEvenTheirOwnHours()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarber(world);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.NotOwnRow);
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenAPlainStaffMemberWritesTheirOwnHours_ShouldRefuse()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarber(world, StaffRoleCodes);

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(world.UnitOfWork.Entities<StaffWorkHour>()).IsEmpty();
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenAStaffSeniorReplacesTheirOwnWeek_ShouldListTheUncoveredAppointments()
    {
        // Arrange
        var world = new StaffWorld();
        ActAsBarber(world, StaffSeniorRoleCodes);
        // Monday 12 October, 14:00 to 14:30 in Athens.
        var outside = world.SeedAppointment(world.BarberMember, new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.Data!.WorkHours).IsEquivalentTo([new StaffWorkHourViewDto(1, "09:00", "13:00")]);
        await Assert.That(result.Data.Conflicts.Select(conflict => conflict.AppointmentId)).IsEquivalentTo([outside.Id]);
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenTheOwnerReplacesTheirOwnWeek_ShouldNeverRefuseOnRank()
    {
        // Arrange
        var world = new StaffWorld();
        // A code the request does not show: on another member's row, a rank rule would refuse.
        world.PermissionResolver.Grant(world.Owner.Id, world.CompanyId, PermissionCodes.Roles.Delete);

        // Act
        var result = await HandleAsync(world, world.OwnerMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.Data!.WorkHours).IsEquivalentTo([new StaffWorkHourViewDto(1, "09:00", "13:00")]);
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenTheCallerHoldsTheWideCode_ShouldWriteAnyonesHours()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, world.ColleagueMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId)])
            .Because("hours move slots, not the catalog");
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenTheWeekIsReplaced_ShouldSoftDeleteTheOldRows()
    {
        // Arrange
        var world = new StaffWorld();
        var oldHour = StaffFactory.CreateWorkHour(world.CompanyId, world.BarberMember.Id, DayOfWeek.Tuesday);
        world.UnitOfWork.Seed(oldHour);

        // Act
        await HandleAsync(world, world.BarberMember.Id, MondayMorning);

        // Assert
        await Assert.That(world.UnitOfWork.Entities<StaffWorkHour>()
                .Where(hour => !hour.IsDeleted && hour.StaffMemberId == world.BarberMember.Id)
                .Select(hour => hour.DayOfWeek))
            .IsEquivalentTo([(short)1]);
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenAnUpcomingAppointmentFallsOutside_ShouldListItAndMoveNothing()
    {
        // Arrange
        var world = new StaffWorld();
        // Monday 12 October, 14:00 to 14:30 in Athens.
        var outside = world.SeedAppointment(world.BarberMember, new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc));
        // Monday 12 October, 10:00 in Athens: inside the new hours.
        world.SeedAppointment(world.BarberMember, new DateTime(2026, 10, 12, 7, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await HandleAsync(world, world.BarberMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.Data!.Conflicts).IsEquivalentTo(
            [new StaffConflictViewDto(outside.Id, new DateOnly(2026, 10, 12), "14:00", "14:30", "Γιώργος Π.")]);
        await Assert.That(outside.StartsAt).IsEqualTo(new DateTime(2026, 10, 12, 11, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    [Arguments(0, "09:00", "10:00")]
    [Arguments(8, "09:00", "10:00")]
    [Arguments(1, "10:00", "09:00")]
    [Arguments(1, "09:03", "10:00")]
    [Arguments(1, "9am", "10:00")]
    public async Task HandleUpdateStaffWorkHours_WhenARangeIsMalformed_ShouldNameIt(int day, string start, string end)
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(
            world, world.BarberMember.Id, new StaffWorkHourAddEditDto { DayOfWeek = day, StartTime = start, EndTime = end });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Staff.InvalidWorkHours);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("WorkHours[0]");
    }

    [Test]
    public async Task HandleUpdateStaffWorkHours_WhenAPlatformAdministratorOnAnotherShopWritesTheirOwn_ShouldSaveAtTheirShop()
    {
        // Arrange
        var world = new StaffWorld();
        var ownMember = world.ActAsPlatformAdministratorFromAnotherShop();

        // Act
        var result = await HandleAsync(world, ownMember.Id, MondayMorning);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<StaffWorkHour>().Select(hour => hour.OwnerId))
            .IsEquivalentTo([ownMember.OwnerId]);
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(ownMember.OwnerId)]);
    }

    private static void ActAsBarber(StaffWorld world, params string[] codes)
    {
        world.CallerUserId = world.Barber.Id;
        world.Caller.RevokeAll().Grant(codes);
    }

    private static Task<Result<StaffScheduleChangeResponse, HttpError>> HandleAsync(
        StaffWorld world, Guid staffMemberId, params StaffWorkHourAddEditDto[] workHours) =>
        new UpdateStaffWorkHoursHandler(
                world.UnitOfWork, world.Outbox, world.UserContext, world.Caller, world.ConstraintReader, world.Clock)
            .ValidateAndHandleAsync(new UpdateStaffWorkHoursRequest
            {
                CompanyId = world.CompanyId,
                CallerUserId = world.CallerUserId,
                StaffMemberId = staffMemberId,
                WorkHours = workHours
            });
}
