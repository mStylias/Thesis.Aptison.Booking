using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aptison.Booking.Application.IntegrationTests.Auth.Authorization;

/// <summary>A grant counts only in the scope it was made in; the bypass flag comes from global grants.</summary>
public class PermissionResolverTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task ResolveAsync_WhenAShopRoleSitsBesideAGlobalGrant_ShouldNotCountTheGlobalCodesAtTheShop()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var barber = UserFactory.Create();
        var shopRole = RoleFactory.Create(companyId: company.Id);

        ActAsNobody();
        await SeedAsync(company, barber);
        await SeedAsync(
            shopRole,
            RoleFactory.CreatePermissionLink(shopRole.Id, await GetPermissionIdAsync(PermissionCodes.Appointments.ReadAll)),
            RoleFactory.CreateGrant(barber.Id, shopRole.Id, company.Id),
            RoleFactory.CreateGrant(barber.Id, TestIds.Roles.Staff));

        // Act
        var resolved = await CreateResolver().ResolveAsync(barber.Id, company.Id, CancellationToken.None);

        // Assert
        await Assert.That(resolved.Codes).Contains(PermissionCodes.Appointments.ReadAll);
        await Assert.That(resolved.Codes).DoesNotContain(PermissionCodes.Appointments.Create)
            .Because("the staff role is granted globally here, and a global grant does not count at a company");
    }

    [Test]
    public async Task ResolveAsync_WhenRolesReadIsGrantedAtAShop_ShouldNotCountItAtPlatformScope()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var admin = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(company, admin);
        await SeedAsync(RoleFactory.CreateGrant(admin.Id, TestIds.Roles.CompanyAdmin, company.Id));

        // Act
        var resolver = CreateResolver();
        var atShop = await resolver.ResolveAsync(admin.Id, company.Id, CancellationToken.None);
        var atPlatform = await resolver.ResolveAsync(admin.Id, companyId: null, CancellationToken.None);

        // Assert
        await Assert.That(atShop.Codes).Contains(PermissionCodes.Roles.Read);
        await Assert.That(atPlatform.Codes).DoesNotContain(PermissionCodes.Roles.Read);
    }

    [Test]
    public async Task ResolveAsync_WhenPlatformManageIsHeldGlobally_ShouldFlagThePlatformAdministratorAtAnyCompany()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var superAdmin = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(company, superAdmin);
        await SeedAsync(RoleFactory.CreateGrant(superAdmin.Id, TestIds.Roles.SuperAdmin));

        // Act
        var resolved = await CreateResolver().ResolveAsync(superAdmin.Id, company.Id, CancellationToken.None);

        // Assert
        await Assert.That(resolved.IsPlatformAdministrator).IsTrue();
        await Assert.That(resolved.Codes).IsEmpty()
            .Because("the bypass comes from the flag, not from global codes leaking into the company");
    }

    [Test]
    public async Task ResolveAsync_WhenTheSeededStaffRoleIsGrantedAtAShop_ShouldAllowCreatingAppointments()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var barber = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(company, barber);
        await SeedAsync(RoleFactory.CreateGrant(barber.Id, TestIds.Roles.Staff, company.Id));

        // Act
        var resolved = await CreateResolver().ResolveAsync(barber.Id, company.Id, CancellationToken.None);

        // Assert
        await Assert.That(resolved.Codes).Contains(PermissionCodes.Appointments.Create);
    }

    private PermissionResolver CreateResolver() =>
        new(
            new PassThroughPermissionCache(),
            new PermissionStore(DbContext, UserContext),
            NullLogger<PermissionResolver>.Instance);

    private Task<Guid> GetPermissionIdAsync(string code) =>
        DbContext.Set<Permission>()
            .Where(permission => permission.Code == code)
            .Select(permission => permission.Id)
            .FirstAsync();
}
