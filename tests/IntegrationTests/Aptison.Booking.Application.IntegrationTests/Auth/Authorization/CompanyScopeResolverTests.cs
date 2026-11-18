using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aptison.Booking.Application.IntegrationTests.Auth.Authorization;

/// <summary>
/// Which company a request resolves to, read through the real store so the staff-row and deleted-
/// company filters are Postgres's.
/// </summary>
public class CompanyScopeResolverTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private static readonly CompanyScopeMetadata RequiredScope = new(Required: true);
    private static readonly CompanyScopeMetadata OptionalScope = new(Required: false);

    [Test]
    public async Task ResolveAsync_WhenTheUserIsActiveStaff_ShouldScopeToTheStaffCompany()
    {
        // Arrange
        var (companyId, userId) = await SeedStaffAsync();

        // Act
        var resolution = await CreateResolver().ResolveAsync(userId, RequiredScope, null, CancellationToken.None);

        // Assert
        var resolved = await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.Resolved>();
        await Assert.That(resolved!.CompanyId).IsEqualTo(companyId);
    }

    [Test]
    public async Task ResolveAsync_WhenTheStaffRowIsInactive_ShouldReportTheScopeMissing()
    {
        // Arrange
        var (_, userId) = await SeedStaffAsync(isActive: false);

        // Act
        var resolution = await CreateResolver().ResolveAsync(userId, RequiredScope, null, CancellationToken.None);

        // Assert
        await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.ScopeMissing>();
    }

    [Test]
    public async Task ResolveAsync_WhenTheStaffRowIsDeleted_ShouldReportTheScopeMissing()
    {
        // Arrange
        var (companyId, userId) = await SeedStaffAsync();

        ActAsCompany(companyId);
        UnitOfWork.GetRepository<StaffMember>().Remove(
            await DbContext.Set<StaffMember>().FirstAsync(member => member.UserId == userId));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var resolution = await CreateResolver().ResolveAsync(userId, RequiredScope, null, CancellationToken.None);

        // Assert
        await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.ScopeMissing>();
    }

    [Test]
    public async Task ResolveAsync_WhenTheStaffCompanyIsDeleted_ShouldReportTheScopeMissing()
    {
        // Arrange
        var (companyId, userId) = await SeedStaffAsync();

        ActAsNobody();
        UnitOfWork.GetRepository<Company>().Remove(
            await DbContext.Set<Company>().FirstAsync(company => company.Id == companyId));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var resolution = await CreateResolver().ResolveAsync(userId, RequiredScope, null, CancellationToken.None);

        // Assert
        await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.ScopeMissing>();
    }

    [Test]
    public async Task ResolveAsync_WhenTheScopeIsOptionalAndTheUserIsNotStaff_ShouldResolvePlatformScope()
    {
        // Arrange
        var user = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(user, RoleFactory.CreateGrant(user.Id, TestIds.Roles.Staff));

        // Act
        var resolution = await CreateResolver().ResolveAsync(user.Id, OptionalScope, null, CancellationToken.None);

        // Assert
        var resolved = await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.Resolved>();
        await Assert.That(resolved!.CompanyId).IsNull();
        await Assert.That(resolved.Permissions.Codes).Contains(PermissionCodes.Appointments.Create)
            .Because("with no company, the global grants are the ones that count");
    }

    [Test]
    public async Task ResolveAsync_WhenTheEndpointDeclaresNoScope_ShouldIgnoreTheStaffCompany()
    {
        // Arrange
        var (_, userId) = await SeedStaffAsync();

        // Act
        var resolution = await CreateResolver().ResolveAsync(userId, scope: null, null, CancellationToken.None);

        // Assert
        var resolved = await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.Resolved>();
        await Assert.That(resolved!.CompanyId).IsNull();
    }

    [Test]
    public async Task ResolveAsync_WhenAPlatformAdministratorNamesACompany_ShouldScopeToIt()
    {
        // Arrange
        var company = CompanyFactory.Create(isActive: false);
        var superAdminId = await SeedPlatformAdministratorAsync();

        ActAsNobody();
        await SeedAsync(company);

        // Act
        var resolution = await CreateResolver().ResolveAsync(
            superAdminId, RequiredScope, company.Id.ToString(), CancellationToken.None);

        // Assert
        var resolved = await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.Resolved>();
        await Assert.That(resolved!.CompanyId).IsEqualTo(company.Id)
            .Because("an inactive company is still reachable by the superadmin");
        await Assert.That(resolved.Permissions.IsPlatformAdministrator).IsTrue();
    }

    [Test]
    public async Task ResolveAsync_WhenAPlatformAdministratorNamesAnUnknownCompany_ShouldReportItNotFound()
    {
        // Arrange
        var superAdminId = await SeedPlatformAdministratorAsync();

        // Act
        var resolution = await CreateResolver().ResolveAsync(
            superAdminId, RequiredScope, Guid.NewGuid().ToString(), CancellationToken.None);

        // Assert
        await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.CompanyNotFound>();
    }

    [Test]
    public async Task ResolveAsync_WhenAPlatformAdministratorNamesNoCompany_ShouldReportTheScopeMissing()
    {
        // Arrange
        var superAdminId = await SeedPlatformAdministratorAsync();

        // Act
        var resolution = await CreateResolver().ResolveAsync(superAdminId, RequiredScope, null, CancellationToken.None);

        // Assert
        await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.ScopeMissing>();
    }

    [Test]
    public async Task ResolveAsync_WhenAStaffMemberNamesAnotherCompany_ShouldIgnoreTheHeader()
    {
        // Arrange
        var (companyId, userId) = await SeedStaffAsync();
        var otherCompany = CompanyFactory.Create();

        ActAsNobody();
        await SeedAsync(otherCompany);

        // Act
        var resolution = await CreateResolver().ResolveAsync(
            userId, RequiredScope, otherCompany.Id.ToString(), CancellationToken.None);

        // Assert
        var resolved = await Assert.That(resolution).IsTypeOf<CompanyScopeResolution.Resolved>();
        await Assert.That(resolved!.CompanyId).IsEqualTo(companyId);
    }

    private CompanyScopeResolver CreateResolver() =>
        new(
            new PermissionResolver(
                new PassThroughPermissionCache(),
                new PermissionStore(DbContext, UserContext),
                NullLogger<PermissionResolver>.Instance),
            DbContext);

    private async Task<(Guid CompanyId, Guid UserId)> SeedStaffAsync(bool isActive = true)
    {
        var company = CompanyFactory.Create();
        var user = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(company, user);
        await SeedAsync(StaffFactory.Create(company.Id, user.Id, isActive: isActive));

        return (company.Id, user.Id);
    }

    private async Task<Guid> SeedPlatformAdministratorAsync()
    {
        var superAdmin = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(superAdmin, RoleFactory.CreateGrant(superAdmin.Id, TestIds.Roles.SuperAdmin));

        return superAdmin.Id;
    }
}
