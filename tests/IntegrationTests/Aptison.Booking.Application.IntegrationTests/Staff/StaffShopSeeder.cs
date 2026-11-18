using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Aptison.Booking.Application.IntegrationTests.Staff;

public sealed record StaffShop(
    Guid CompanyId,
    Guid OwnerUserId,
    Guid OwnerMemberId,
    Guid BarberUserId,
    Guid BarberMemberId,
    Guid ServiceId,
    Guid CustomerId,
    Guid OtherCompanyId);

/// <summary>
/// An Athens shop, open 09:00 to 17:00 every day: the owner, holding <c>company_admin</c> here, and
/// a barber holding <c>staff</c>, with one service. Another shop beside it. The test acts as the
/// owner, with part of the codes <c>company_admin</c> holds.
/// </summary>
public static class StaffShopSeeder
{
    public static readonly string[] CompanyAdminCodes =
    [
        PermissionCodes.Members.Read, PermissionCodes.Members.Invite, PermissionCodes.Members.Update,
        PermissionCodes.Members.Remove, PermissionCodes.Services.Read, PermissionCodes.Staff.ManageSchedule,
        PermissionCodes.Staff.ManageServices, PermissionCodes.Staff.ManageTimeOff,
        PermissionCodes.Appointments.ReadAll, PermissionCodes.Appointments.Create,
        PermissionCodes.Appointments.Update, PermissionCodes.Appointments.Cancel, PermissionCodes.Roles.Read,
        PermissionCodes.Roles.Assign
    ];

    public static async Task<StaffShop> SeedAsync(DatabaseTest test, IServiceProvider services)
    {
        var company = CompanyFactory.Create();
        var otherCompany = CompanyFactory.Create();
        var owner = UserFactory.Create(firstName: "Όλγα", lastName: "Ιδιοκτήτρια", keycloakId: Guid.CreateVersion7());
        var barber = UserFactory.Create(firstName: "Nikos", lastName: "Barberis", keycloakId: Guid.CreateVersion7());
        var customer = UserFactory.Create(firstName: "Γιώργος", lastName: "Πελάτης");

        test.ActAsNobody();
        await test.SeedAsync(company, otherCompany, owner, barber, customer);

        test.ActAsCompany(company.Id);
        var ownerMember = StaffFactory.Create(company.Id, owner.Id, isBookable: false);
        var barberMember = StaffFactory.Create(company.Id, barber.Id);
        var service = ServiceFactory.Create(company.Id);
        await test.SeedAsync(ownerMember, barberMember, service);
        await test.SeedRangeAsync(CompanyFactory.CreateWeeklyBusinessHours(company.Id));
        await test.SeedAsync(
            ServiceFactory.CreateTranslation(service.Id, "Κούρεμα"),
            RoleFactory.CreateGrant(owner.Id, TestIds.Roles.CompanyAdmin, company.Id),
            RoleFactory.CreateGrant(barber.Id, TestIds.Roles.Staff, company.Id));

        test.ActAs(owner.Id, company.Id);
        services.GetRequiredService<FakeCurrentPermissions>().Grant(CompanyAdminCodes);
        services.GetRequiredService<FakePermissionResolver>()
            .Grant(owner.Id, company.Id, CompanyAdminCodes)
            .Grant(barber.Id, company.Id, PermissionCodes.Appointments.ReadAll);

        return new StaffShop(
            company.Id, owner.Id, ownerMember.Id, barber.Id, barberMember.Id, service.Id, customer.Id, otherCompany.Id);
    }
}
