using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Persistence;

/// <summary>
/// The harness itself. If these fail, nothing else in this project means anything.
/// </summary>
public class HarnessSmokeTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    /// <summary>Fixed, so the rollback test below can look for exactly this row.</summary>
    private static readonly Guid RolledBackCompanyId = new("ffffffff-0000-4000-8000-000000000001");

    [Test]
    public async Task SeedCompany_WhenWrittenInsideTheTest_ShouldBeReadableBack()
    {
        // Arrange
        var company = CompanyFactory.Create();

        // Act
        await SeedAsync(company);

        // Assert
        var found = await FindAsync<Company>(company.Id);
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Slug).IsEqualTo(company.Slug);
    }

    [Test]
    public async Task RunMigrations_WhenTheContainerStarts_ShouldInsertTheReferenceRows()
    {
        // Arrange
        // The migrations are the only thing that has run against this database.

        // Act
        var greek = await FindAsync<Language>(TestIds.Languages.Greek);

        // Assert
        await Assert.That(greek).IsNotNull();
        await Assert.That(greek!.Code).IsEqualTo("el");
    }

    [Test]
    public async Task SeedCompany_WhenTheTestEnds_ShouldLeaveNothingBehind()
    {
        // Arrange
        // Act
        await SeedAsync(CompanyFactory.Create(RolledBackCompanyId));

        // Assert
        await Assert.That(await FindAsync<Company>(RolledBackCompanyId)).IsNotNull();
    }

    /// <summary>
    /// The one test that proves the mechanism everything else rests on: the row the test above
    /// committed is gone, because its transaction was rolled back.
    /// </summary>
    [Test]
    [DependsOn(nameof(SeedCompany_WhenTheTestEnds_ShouldLeaveNothingBehind))]
    public async Task ReadCompany_WhenAnEarlierTestWroteItAndRolledBack_ShouldFindNothing()
    {
        // Arrange
        // Act
        var found = await FindAsync<Company>(RolledBackCompanyId);

        // Assert
        await Assert.That(found).IsNull();
    }
}
