using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aptison.Booking.Application.IntegrationTests.Staff;

/// <summary>
/// What a member may read and write of their own schedule: the own time-off read against real SQL,
/// and the seeded role codes the write endpoints check.
/// </summary>
public class StaffOwnScheduleTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private static readonly string[] ScheduleCodes =
    [
        PermissionCodes.Members.Read, PermissionCodes.Staff.ManageSchedule, PermissionCodes.Staff.ManageOwnSchedule,
        PermissionCodes.Staff.ManageTimeOff, PermissionCodes.Staff.ManageOwnTimeOff
    ];

    [Test]
    public async Task ListStaffTimeOff_WhenAPlainStaffMemberReadsTheirOwnAndAColleagues_ShouldListOnlyTheirOwn()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        ActAsCompany(shop.CompanyId);
        var ownTimeOff = StaffFactory.CreateTimeOff(
            shop.CompanyId, shop.BarberMemberId, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4));
        await SeedAsync(
            ownTimeOff,
            StaffFactory.CreateTimeOff(shop.CompanyId, shop.OwnerMemberId, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6)));

        ActAs(shop.BarberUserId, shop.CompanyId);
        Services.GetRequiredService<FakeCurrentPermissions>().RevokeAll().Grant(PermissionCodes.Appointments.ReadAll);

        // Act
        var own = await Sender.SendAsync(new ListStaffTimeOffRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = shop.BarberUserId, StaffMemberId = shop.BarberMemberId
        });
        var colleagues = await Sender.SendAsync(new ListStaffTimeOffRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = shop.BarberUserId, StaffMemberId = shop.OwnerMemberId
        });

        // Assert
        await Assert.That(own.Data!.Select(timeOff => timeOff.Id)).IsEquivalentTo([ownTimeOff.Id]);
        await Assert.That(colleagues.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(colleagues.Error.ErrorCode).IsEqualTo(ErrorCodes.Authorization.PermissionDenied);
    }

    [Test]
    public async Task ListStaffTimeOff_WhenTheIdBelongsToAnotherShop_ShouldRefuseAPlainStaffMember()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var stranger = UserFactory.Create(keycloakId: Guid.CreateVersion7());
        ActAsNobody();
        await SeedAsync(stranger);
        ActAsCompany(shop.OtherCompanyId);
        var strangerMember = StaffFactory.Create(shop.OtherCompanyId, stranger.Id);
        await SeedAsync(strangerMember);

        ActAs(shop.BarberUserId, shop.CompanyId);
        Services.GetRequiredService<FakeCurrentPermissions>().RevokeAll();

        // Act
        var result = await Sender.SendAsync(new ListStaffTimeOffRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = shop.BarberUserId, StaffMemberId = strangerMember.Id
        });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
    }

    [Test]
    public async Task ListStaffTimeOff_WhenAPlatformAdministratorOnAnotherShopReadsTheirOwn_ShouldListIt()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var (administratorUserId, ownMemberId) = await SeedPlatformAdministratorAsync(shop.OtherCompanyId);
        var ownTimeOff = StaffFactory.CreateTimeOff(
            shop.OtherCompanyId, ownMemberId, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4));
        await SeedAsync(ownTimeOff);

        ActAsPlatformAdministrator(administratorUserId, shop.CompanyId);

        // Act
        var result = await Sender.SendAsync(new ListStaffTimeOffRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = administratorUserId, StaffMemberId = ownMemberId
        });

        // Assert
        await Assert.That(result.Data!.Select(timeOff => timeOff.Id)).IsEquivalentTo([ownTimeOff.Id])
            .Because("the own row lives at the membership's shop, not at the shop in the header");
    }

    [Test]
    public async Task CreateStaffTimeOff_WhenAPlatformAdministratorOnAnotherShopAddsTheirOwn_ShouldStampTheirShop()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var (administratorUserId, ownMemberId) = await SeedPlatformAdministratorAsync(shop.OtherCompanyId);
        ActAsPlatformAdministrator(administratorUserId, shop.CompanyId);
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd");

        // Act
        var result = await Sender.SendAsync(new CreateStaffTimeOffRequest
        {
            CompanyId = shop.CompanyId,
            CallerUserId = administratorUserId,
            StaffMemberId = ownMemberId,
            StartsOn = day,
            EndsOn = day
        });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var storedOwnerId = await DbContext.Set<StaffTimeOff>()
            .IgnoreQueryFilters()
            .Where(timeOff => timeOff.Id == result.Data!.TimeOff!.Id)
            .Select(timeOff => timeOff.OwnerId)
            .SingleAsync();
        await Assert.That(storedOwnerId).IsEqualTo(shop.OtherCompanyId);
    }

    [Test]
    [Arguments("staff", new string[0])]
    [Arguments("staff_senior", new[] { "members:read", "staff:manage_own_schedule", "staff:manage_own_timeoff" })]
    [Arguments(
        "company_admin",
        new[] { "members:read", "staff:manage_schedule", "staff:manage_own_schedule", "staff:manage_timeoff", "staff:manage_own_timeoff" })]
    public async Task ReadRoleCodes_WhenTheSystemRoleIsSeeded_ShouldGrantTheExpectedScheduleCodes(
        string roleName, string[] expectedCodes)
    {
        // Arrange
        var roleId = roleName switch
        {
            "staff" => TestIds.Roles.Staff,
            "staff_senior" => TestIds.Roles.StaffSenior,
            _ => TestIds.Roles.CompanyAdmin
        };

        // Act
        var scheduleCodes = await DbContext.Set<RolePermission>()
            .Where(link => link.RoleId == roleId && ScheduleCodes.Contains(link.Permission.Code))
            .Select(link => link.Permission.Code)
            .ToListAsync();

        // Assert
        await Assert.That(scheduleCodes).IsEquivalentTo(expectedCodes)
            .Because("the profile page's write rules rest on these codes");
    }

    private async Task<(Guid UserId, Guid MemberId)> SeedPlatformAdministratorAsync(Guid homeCompanyId)
    {
        var administrator = UserFactory.Create(keycloakId: Guid.CreateVersion7());
        ActAsNobody();
        await SeedAsync(administrator);
        ActAsCompany(homeCompanyId);
        var member = StaffFactory.Create(homeCompanyId, administrator.Id);
        await SeedAsync(member);
        return (administrator.Id, member.Id);
    }

    /// <summary>Works on <paramref name="companyId"/> as the header would name it.</summary>
    private void ActAsPlatformAdministrator(Guid userId, Guid companyId)
    {
        ActAs(userId, companyId);
        Services.GetRequiredService<FakeCurrentPermissions>().RevokeAll().GrantPlatformAdministrator();
    }
}
