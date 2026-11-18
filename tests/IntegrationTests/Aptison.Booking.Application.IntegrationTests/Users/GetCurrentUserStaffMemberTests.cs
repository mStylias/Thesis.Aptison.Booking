using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Users;

/// <summary>
/// The caller's own staff record against real SQL: the read spans companies, so the ownership filter
/// is lifted and only the user and soft-delete filters decide the row.
/// </summary>
public class GetCurrentUserStaffMemberTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task GetCurrentUserStaffMember_WhenAPlainStaffMemberAsks_ShouldReturnTheirLiveHoursInOneRoundTrip()
    {
        // Arrange
        var company = CompanyFactory.Create(slotIntervalMinutes: 20);
        var barber = UserFactory.Create(firstName: "Άρης", lastName: "Δημάκης", keycloakId: Guid.CreateVersion7());
        var colleague = UserFactory.Create(firstName: "Μαρία", lastName: "Συνάδελφος", keycloakId: Guid.CreateVersion7());

        ActAsNobody();
        await SeedAsync(company, barber, colleague);

        ActAsCompany(company.Id);
        var member = StaffFactory.Create(company.Id, barber.Id);
        var colleagueMember = StaffFactory.Create(company.Id, colleague.Id);
        await SeedAsync(member, colleagueMember, RoleFactory.CreateGrant(barber.Id, TestIds.Roles.Staff, company.Id));

        var removedTuesday = StaffFactory.CreateWorkHour(company.Id, member.Id, DayOfWeek.Tuesday);
        removedTuesday.IsDeleted = true;
        var closedSunday = CompanyFactory.CreateBusinessHour(company.Id, DayOfWeek.Sunday);
        closedSunday.IsDeleted = true;
        await SeedAsync(
            StaffFactory.CreateWorkHour(company.Id, member.Id, DayOfWeek.Monday, new TimeOnly(15, 0), new TimeOnly(19, 0)),
            StaffFactory.CreateWorkHour(company.Id, member.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            StaffFactory.CreateWorkHour(company.Id, member.Id, DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(14, 0)),
            removedTuesday,
            StaffFactory.CreateWorkHour(company.Id, colleagueMember.Id, DayOfWeek.Wednesday),
            CompanyFactory.CreateBusinessHour(company.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(20, 0)),
            CompanyFactory.CreateBusinessHour(company.Id, DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(16, 0)),
            closedSunday);

        ActAsPlatform(barber.Id);
        var commandCountBefore = GetSqlCommandCount();

        // Act
        var result = await Sender.SendAsync(new GetCurrentUserStaffMemberRequest { UserId = barber.Id });

        // Assert
        await Assert.That(GetSqlCommandCount() - commandCountBefore).IsEqualTo(1);

        var staffMember = result.Data!;
        await Assert.That(staffMember.StaffMemberId).IsEqualTo(member.Id);
        await Assert.That(staffMember.CompanyId).IsEqualTo(company.Id);
        await Assert.That(staffMember.DisplayName).IsEqualTo("Άρης Δ.");
        await Assert.That(staffMember.JobTitleCode).IsEqualTo("barber");
        await Assert.That(staffMember.IsBookable).IsTrue();
        await Assert.That(staffMember.Timezone).IsEqualTo("Europe/Athens");
        await Assert.That(staffMember.SlotIntervalMinutes).IsEqualTo(20);
        await Assert.That(staffMember.WorkHours).IsEquivalentTo(
            [
                new StaffWorkHourViewDto(1, "09:00", "13:00"),
                new StaffWorkHourViewDto(1, "15:00", "19:00"),
                new StaffWorkHourViewDto(7, "10:00", "14:00")
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(staffMember.BusinessHours).IsEquivalentTo(
            [new BusinessHourViewDto(1, "09:00", "20:00"), new BusinessHourViewDto(6, "10:00", "16:00")],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetCurrentUserStaffMember_WhenTheCallerIsACustomer_ShouldReturnNotStaffMember()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var customer = UserFactory.Create(keycloakId: Guid.CreateVersion7());
        await SeedAccountsAsync(customer);
        await SeedMemberAsync(company, await SeedAccountAsync());
        ActAsPlatform(customer.Id);

        // Act
        var result = await Sender.SendAsync(new GetCurrentUserStaffMemberRequest { UserId = customer.Id });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Users.NotStaffMember);
    }

    [Test]
    public async Task GetCurrentUserStaffMember_WhenTheMembershipIsDeactivated_ShouldReturnNotStaffMember()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var barber = await SeedAccountAsync();
        await SeedMemberAsync(company, barber, isActive: false);
        ActAsPlatform(barber.Id);

        // Act
        var result = await Sender.SendAsync(new GetCurrentUserStaffMemberRequest { UserId = barber.Id });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Users.NotStaffMember);
    }

    [Test]
    public async Task GetCurrentUserStaffMember_WhenAnOldMembershipWasDeleted_ShouldReturnTheActiveOne()
    {
        // Arrange
        var oldCompany = await SeedCompanyAsync();
        var company = await SeedCompanyAsync();
        var barber = await SeedAccountAsync();
        await SeedMemberAsync(oldCompany, barber, isActive: false, isDeleted: true);
        var member = await SeedMemberAsync(company, barber);
        ActAsPlatform(barber.Id);

        // Act
        var result = await Sender.SendAsync(new GetCurrentUserStaffMemberRequest { UserId = barber.Id });

        // Assert
        await Assert.That(result.Data!.StaffMemberId).IsEqualTo(member.Id);
    }

    [Test]
    public async Task GetCurrentUserStaffMember_WhenAPlatformAdministratorWorksOnAnotherShop_ShouldReturnTheirOwnRecord()
    {
        // Arrange
        var ownCompany = await SeedCompanyAsync();
        var otherCompany = await SeedCompanyAsync();
        var administrator = await SeedAccountAsync();
        await SeedAsync(RoleFactory.CreateGrant(administrator.Id, TestIds.Roles.SuperAdmin));
        var member = await SeedMemberAsync(ownCompany, administrator);
        await SeedMemberAsync(otherCompany, await SeedAccountAsync());
        // What the company header gives any company-scoped request.
        ActAs(administrator.Id, otherCompany.Id);

        // Act
        var result = await Sender.SendAsync(new GetCurrentUserStaffMemberRequest { UserId = administrator.Id });

        // Assert
        await Assert.That(result.Data!.StaffMemberId).IsEqualTo(member.Id);
        await Assert.That(result.Data.CompanyId).IsEqualTo(ownCompany.Id);
    }

    [Test]
    public async Task GetCurrentUserStaffMember_WhenAPlatformAdministratorHasNoMembership_ShouldReturnNotStaffMember()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var administrator = await SeedAccountAsync();
        await SeedAsync(RoleFactory.CreateGrant(administrator.Id, TestIds.Roles.SuperAdmin));
        await SeedMemberAsync(company, await SeedAccountAsync());

        // Act
        ActAs(administrator.Id, company.Id);
        var withCompany = await Sender.SendAsync(new GetCurrentUserStaffMemberRequest { UserId = administrator.Id });
        ActAsPlatform(administrator.Id);
        var withoutCompany = await Sender.SendAsync(new GetCurrentUserStaffMemberRequest { UserId = administrator.Id });

        // Assert
        await Assert.That(withCompany.Error!.ErrorCode).IsEqualTo(ErrorCodes.Users.NotStaffMember);
        await Assert.That(withoutCompany.Error!.ErrorCode).IsEqualTo(ErrorCodes.Users.NotStaffMember);
    }

    private async Task<Company> SeedCompanyAsync()
    {
        var company = CompanyFactory.Create();
        ActAsNobody();
        await SeedAsync(company);
        return company;
    }

    private async Task<User> SeedAccountAsync()
    {
        var user = UserFactory.Create(keycloakId: Guid.CreateVersion7());
        await SeedAccountsAsync(user);
        return user;
    }

    /// <summary>Outside any company, or the interceptor would stamp the account as a walk-in.</summary>
    private async Task SeedAccountsAsync(params User[] users)
    {
        ActAsNobody();
        await SeedAsync(users);
    }

    private async Task<StaffMember> SeedMemberAsync(
        Company company, User user, bool isActive = true, bool isDeleted = false)
    {
        var member = StaffFactory.Create(company.Id, user.Id, isActive: isActive);
        member.IsDeleted = isDeleted;
        ActAsCompany(company.Id);
        await SeedAsync(member);
        ActAsNobody();
        return member;
    }
}
