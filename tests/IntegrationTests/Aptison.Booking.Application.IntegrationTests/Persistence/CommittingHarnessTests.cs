using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Persistence;

/// <summary>
/// The escape hatch's own cleanup. A committing test's rows are real, so only the truncate stops
/// them reaching the next test.
/// </summary>
public class CommittingHarnessTests(BookingDatabase database) : CommittingDatabaseTest(database)
{
    private static readonly Guid TruncatedCompanyId = new("ffffffff-0000-4000-8000-000000000002");

    [Test]
    public async Task SeedCompany_WhenTheTestCommits_ShouldWriteItForReal()
    {
        // Arrange
        // Act
        ActAsNobody();
        await SeedAsync(CompanyFactory.Create(TruncatedCompanyId));

        // Assert
        await Assert.That(await FindAsync<Company>(TruncatedCompanyId)).IsNotNull();
    }

    [Test]
    [DependsOn(nameof(SeedCompany_WhenTheTestCommits_ShouldWriteItForReal))]
    public async Task ReadCompany_WhenAnEarlierCommittingTestWroteIt_ShouldFindItTruncatedAway()
    {
        // Arrange
        // Act
        var found = await FindAsync<Company>(TruncatedCompanyId);

        // Assert
        await Assert.That(found).IsNull();
    }

    [Test]
    [DependsOn(nameof(SeedCompany_WhenTheTestCommits_ShouldWriteItForReal))]
    public async Task ReadLanguages_WhenAnEarlierTestTruncated_ShouldKeepTheReferenceRows()
    {
        // Arrange
        // Act
        var greek = await FindAsync<Language>(TestIds.Languages.Greek);

        // Assert
        // The truncate must spare what the migrations inserted: they are foreign keys every other
        // test depends on, and nothing re-inserts them.
        await Assert.That(greek).IsNotNull();
    }
}
