using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Staff;

/// <summary>
/// Adding, deactivating and deleting members against the real schema: the <c>citext</c> lookup,
/// the ownership and soft-delete filters, the seeded roles and the staff row the permission cache
/// reads.
/// </summary>
public class StaffMembershipTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task FindStaffCandidate_WhenTheAddressDiffersInCaseAndSpacing_ShouldFindThePerson()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        await SeedCandidateAsync(shop, "New.Barber@Example.test");

        // Act
        var result = await Sender.SendAsync(
            new FindStaffCandidateRequest { CompanyId = shop.CompanyId, Email = "  new.barber@EXAMPLE.test " });

        // Assert
        await Assert.That(result.Data).IsEqualTo(new StaffCandidateResponse("Eleni", "Nea", "New.Barber@Example.test"));
    }

    [Test]
    public async Task FindStaffCandidate_WhenOnlyAWalkInHasTheAddress_ShouldFindNobody()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        await SeedCandidateAsync(shop, "walk-in@example.test", hasLogin: false);

        // Act
        var result = await Sender.SendAsync(
            new FindStaffCandidateRequest { CompanyId = shop.CompanyId, Email = "walk-in@example.test" });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.CandidateNotFound);
    }

    [Test]
    public async Task CreateStaffMember_WhenTheBodyIsValid_ShouldLetTheNewMembersNextRequestResolveTheCompany()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var candidateId = await SeedCandidateAsync(shop, "new.barber@example.test");

        // Act
        var result = await Sender.SendAsync(BuildCreateRequest(shop, "new.barber@example.test", TestIds.Roles.Staff));

        // Assert
        var member = result.Data!;
        await Assert.That(member.Roles).IsEquivalentTo([new StaffRoleViewDto(TestIds.Roles.Staff, "staff")]);
        await Assert.That(member.Services).IsEquivalentTo([new StaffMemberServiceViewDto(shop.ServiceId, null, null)]);
        await Assert.That(member.BookingBlockers).IsEquivalentTo(["NO_WORK_HOURS"]);

        await Assert.That(Scope.GetRequiredService<FakePermissionCacheInvalidator>().InvalidatedUsersIds)
            .IsEquivalentTo([candidateId]);
        var access = await new PermissionStore(DbContext, UserContext).ReadUserAccessAsync(candidateId, CancellationToken.None);
        await Assert.That(access.StaffCompanyId).IsEqualTo(shop.CompanyId);
        await Assert.That(access.Grants.Select(grant => (grant.RoleId, grant.OwnerId)))
            .IsEquivalentTo([(TestIds.Roles.Staff, (Guid?)shop.CompanyId)]);

        var calendarChange = (await GetOutboxRowsAsync()).Single();
        await Assert.That(calendarChange.Payload).Contains("\"CatalogChanged\":true");
    }

    [Test]
    public async Task CreateStaffMember_WhenThePersonIsInactiveAtAnotherShop_ShouldAddThem()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var candidateId = await SeedCandidateAsync(shop, "moving@example.test");
        ActAsCompany(shop.OtherCompanyId);
        await SeedAsync(StaffFactory.Create(shop.OtherCompanyId, candidateId, isActive: false));
        ActAs(shop.OwnerUserId, shop.CompanyId);

        // Act
        var result = await Sender.SendAsync(BuildCreateRequest(shop, "moving@example.test"));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var access = await new PermissionStore(DbContext, UserContext).ReadUserAccessAsync(candidateId, CancellationToken.None);
        await Assert.That(access.StaffCompanyId).IsEqualTo(shop.CompanyId);
    }

    [Test]
    public async Task CreateStaffMember_WhenARoleGrantsMoreThanTheCallerHolds_ShouldRefuseTheEscalation()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        await SeedCandidateAsync(shop, "new.barber@example.test");

        // Act
        // company_admin holds roles:create, which the caller does not.
        var result = await Sender.SendAsync(
            BuildCreateRequest(shop, "new.barber@example.test", TestIds.Roles.CompanyAdmin));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Roles.PermissionEscalation);
        await Assert.That(await DbContext.Set<StaffMember>().CountAsync()).IsEqualTo(2);
    }

    [Test]
    public async Task DeactivateStaffMember_WhenOnlyPastAppointmentsRemain_ShouldKeepThemInTheBook()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var past = AppointmentFactory.Create(
            shop.CompanyId, shop.BarberMemberId, shop.CustomerId, DateTime.UtcNow.AddDays(-2).Date.AddHours(8));
        await SeedAsync(past);

        // Act
        var result = await Sender.SendAsync(new DeactivateStaffMemberRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = shop.OwnerUserId, StaffMemberId = shop.BarberMemberId
        });

        // Assert
        await Assert.That(result.Data!.IsActive).IsFalse();

        var book = await Sender.SendAsync(new ListAppointmentsODataRequest());
        var appointments = await book.Data!.ToListAsync();
        await Assert.That(appointments.Select(appointment => appointment.Id)).Contains(past.Id);

        var access = await new PermissionStore(DbContext, UserContext)
            .ReadUserAccessAsync(shop.BarberUserId, CancellationToken.None);
        await Assert.That(access.StaffCompanyId).IsNull().Because("a deactivated barber resolves no company");
        await Assert.That(access.Grants).IsNotEmpty().Because("the grants stay for a reactivation");
    }

    [Test]
    public async Task DeleteStaffMember_WhenNothingNamesThem_ShouldSoftDeleteEverythingHangingOffThem()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        await SeedAsync(
            ServiceFactory.CreateStaffService(shop.CompanyId, shop.BarberMemberId, shop.ServiceId),
            StaffFactory.CreateWorkHour(shop.CompanyId, shop.BarberMemberId, DayOfWeek.Monday),
            StaffFactory.CreateTimeOff(
                shop.CompanyId, shop.BarberMemberId, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)));

        // Act
        var result = await Sender.SendAsync(new DeleteStaffMemberRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = shop.OwnerUserId, StaffMemberId = shop.BarberMemberId
        });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(await DbContext.Set<StaffMember>().AnyAsync(member => member.Id == shop.BarberMemberId)).IsFalse();
        await Assert.That(await DbContext.Set<StaffService>().AnyAsync(row => row.StaffMemberId == shop.BarberMemberId)).IsFalse();
        await Assert.That(await DbContext.Set<StaffWorkHour>().AnyAsync(row => row.StaffMemberId == shop.BarberMemberId)).IsFalse();
        await Assert.That(await DbContext.Set<StaffTimeOff>().AnyAsync(row => row.StaffMemberId == shop.BarberMemberId)).IsFalse();
        await Assert.That(await DbContext.Set<UserRole>().AnyAsync(row => row.UserId == shop.BarberUserId)).IsFalse();

        var deletedRow = await DbContext.Set<StaffMember>()
            .IgnoreQueryFilters()
            .SingleAsync(member => member.Id == shop.BarberMemberId);
        await Assert.That(deletedRow.DeletedById).IsEqualTo(shop.OwnerUserId);
    }

    [Test]
    public async Task ListStaffMembers_WhenTheShopHasAnInactiveMember_ShouldListBothWithTheirBlockers()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        await SeedAsync(ServiceFactory.CreateStaffService(shop.CompanyId, shop.BarberMemberId, shop.ServiceId));
        await SeedRangeAsync(StaffFactory.CreateWeeklyWorkHours(shop.CompanyId, shop.BarberMemberId));

        // Act
        var result = await Sender.SendAsync(new ListStaffMembersRequest { CompanyId = shop.CompanyId });

        // Assert
        var cards = result.Data!;
        await Assert.That(cards.Select(card => card.DisplayName)).IsEquivalentTo(
            ["Nikos B.", "Όλγα Ι."], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(cards[0].BookingBlockers).IsEmpty();
        await Assert.That(cards[0].RoleNames).IsEquivalentTo(["staff"]);
        await Assert.That(cards[1].BookingBlockers).IsEquivalentTo(["NOT_BOOKABLE", "NO_SERVICES", "NO_WORK_HOURS"]);
    }

    [Test]
    public async Task GetStaffOptions_WhenTheCallerIsNotAPlatformAdministrator_ShouldHideThePlatformRoles()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);

        // Act
        var result = await Sender.SendAsync(new GetStaffOptionsRequest { CompanyId = shop.CompanyId });

        // Assert
        await Assert.That(result.Data!.Roles.Select(role => role.Name))
            .IsEquivalentTo(["company_admin", "staff", "staff_admin", "staff_senior"]);
        await Assert.That(result.Data.Services.Single().Name).IsEqualTo("Κούρεμα");
        await Assert.That(result.Data.BusinessHours).Count().IsEqualTo(7);
    }

    private async Task<Guid> SeedCandidateAsync(StaffShop shop, string email, bool hasLogin = true)
    {
        var candidate = UserFactory.Create(
            email: email, firstName: "Eleni", lastName: "Nea", keycloakId: hasLogin ? Guid.CreateVersion7() : null);

        ActAsNobody();
        await SeedAsync(candidate);
        ActAs(shop.OwnerUserId, shop.CompanyId);

        return candidate.Id;
    }

    private static CreateStaffMemberRequest BuildCreateRequest(StaffShop shop, string email, params Guid[] roleIds) =>
        new()
        {
            CompanyId = shop.CompanyId,
            CallerUserId = shop.OwnerUserId,
            Email = email,
            JobTitleCode = "barber",
            IsBookable = true,
            RoleIds = roleIds,
            Services = [new StaffServiceAddEditDto { ServiceId = shop.ServiceId }]
        };
}
