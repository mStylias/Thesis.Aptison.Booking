using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Tests.Common.Data;
using Synax.Identity;

namespace Aptison.Booking.Application.IntegrationTests.Persistence;

/// <summary>
/// Tenant isolation against real SQL. The in-memory fakes emulate these filters; this proves the
/// translated query does the same — and that the accessor is re-evaluated per query rather than
/// baked into cached SQL, which is the failure that leaks one company's rows to another.
/// </summary>
public class OwnershipIsolationTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task QueryService_WhenAnotherCompanyIsInScope_ShouldFindNothing()
    {
        // Arrange
        var (companyA, companyB) = await SeedTwoCompaniesAsync();
        ActAsCompany(companyA);
        var service = ServiceFactory.Create(companyA);
        await SeedAsync(service);

        // Act
        ActAsCompany(companyB);
        var seenByB = await FindAsync<Service>(service.Id);

        // Assert
        await Assert.That(seenByB).IsNull();
        ActAsCompany(companyA);
        await Assert.That(await FindAsync<Service>(service.Id)).IsNotNull();
    }

    [Test]
    public async Task QueryService_WhenTheRouteNamesNoCompany_ShouldFindNothing()
    {
        // Arrange
        var (companyA, _) = await SeedTwoCompaniesAsync();
        ActAsCompany(companyA);
        var service = ServiceFactory.Create(companyA);
        await SeedAsync(service);

        // Act
        // Platform scope. The safe direction is no owned rows, never all of them.
        ActAsNobody();
        var seenAtPlatformScope = await FindAsync<Service>(service.Id);

        // Assert
        await Assert.That(seenAtPlatformScope).IsNull();
    }

    [Test]
    public async Task QueryService_WhenInsideBecomeAllOwners_ShouldFindEveryCompanysRows()
    {
        // Arrange
        var (companyA, companyB) = await SeedTwoCompaniesAsync();
        ActAsCompany(companyA);
        var serviceOfA = ServiceFactory.Create(companyA);
        await SeedAsync(serviceOfA);
        ActAsCompany(companyB);
        var serviceOfB = ServiceFactory.Create(companyB);
        await SeedAsync(serviceOfB);

        // Act
        ActAsNobody();
        using var _ = UserContext.BecomeAllOwners();
        var foundOfA = await FindAsync<Service>(serviceOfA.Id);
        var foundOfB = await FindAsync<Service>(serviceOfB.Id);

        // Assert
        await Assert.That(foundOfA).IsNotNull();
        await Assert.That(foundOfB).IsNotNull();
    }

    [Test]
    public async Task QueryService_WhenScopedToACompany_ShouldFilterInTheDatabase()
    {
        // Arrange
        var (companyA, _) = await SeedTwoCompaniesAsync();
        ActAsCompany(companyA);
        await SeedAsync(ServiceFactory.Create(companyA));

        // Act
        await FindAsync<Service>(Guid.CreateVersion7());

        // Assert
        // Rows coming back right is not the same fact as the filter running in SQL: a filter
        // evaluated client-side reads the whole table first.
        var sql = GetLastSqlMentioning("FROM services");
        await Assert.That(sql).Contains("owner_id");
    }

    private async Task<(Guid CompanyA, Guid CompanyB)> SeedTwoCompaniesAsync()
    {
        var companyA = CompanyFactory.Create();
        var companyB = CompanyFactory.Create();

        // Companies span companies — they are the tenant root, so they carry no owner themselves.
        ActAsNobody();
        await SeedAsync(companyA, companyB);

        return (companyA.Id, companyB.Id);
    }
}
