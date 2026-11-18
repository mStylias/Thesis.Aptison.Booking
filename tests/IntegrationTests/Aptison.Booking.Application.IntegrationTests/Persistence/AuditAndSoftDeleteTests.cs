using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Persistence;

/// <summary>
/// What the interceptors stamp, and what the soft-delete filter hides. Application code never
/// assigns any of these, so nothing but a test says they happen.
/// </summary>
public class AuditAndSoftDeleteTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task InsertService_WhenAUserIsActing_ShouldStampCreatedAtAndCreatedBy()
    {
        // Arrange
        var (companyId, userId) = await SeedCompanyWithUserAsync();
        var service = ServiceFactory.Create(companyId);

        // Act
        await SeedAsync(service);

        // Assert
        var saved = await FindAsync<Service>(service.Id);
        await Assert.That(saved!.CreatedById).IsEqualTo(userId);
        await Assert.That(saved.CreatedAt).IsNotEqualTo(default(DateTime));
        // Null until the first modification — the column carries no DEFAULT now() for this reason.
        await Assert.That(saved.UpdatedAt).IsNull();
    }

    [Test]
    public async Task UpdateService_WhenSaved_ShouldStampUpdatedAtAndModifiedBy()
    {
        // Arrange
        var (companyId, userId) = await SeedCompanyWithUserAsync();
        var service = ServiceFactory.Create(companyId);
        await SeedAsync(service);

        // Act
        var tracked = await DbContext.Set<Service>().FirstAsync(row => row.Id == service.Id);
        tracked.Price = 35m;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        var saved = await FindAsync<Service>(service.Id);
        await Assert.That(saved!.UpdatedAt).IsNotNull();
        await Assert.That(saved.ModifiedById).IsEqualTo(userId);
    }

    [Test]
    public async Task RemoveService_WhenSaved_ShouldSoftDeleteItAndHideItFromQueries()
    {
        // Arrange
        var (companyId, userId) = await SeedCompanyWithUserAsync();
        var service = ServiceFactory.Create(companyId);
        await SeedAsync(service);

        // Act
        UnitOfWork.GetRepository<Service>().Remove(
            await DbContext.Set<Service>().FirstAsync(row => row.Id == service.Id));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Assert
        await Assert.That(await FindAsync<Service>(service.Id)).IsNull();

        var row = await DbContext.Set<Service>()
            .IgnoreQueryFilters()
            .FirstAsync(candidate => candidate.Id == service.Id);

        await Assert.That(row.IsDeleted).IsTrue();
        await Assert.That(row.DeletedById).IsEqualTo(userId);
        await Assert.That(row.DeletedAt).IsNotNull();
    }

    private async Task<(Guid CompanyId, Guid UserId)> SeedCompanyWithUserAsync()
    {
        var company = CompanyFactory.Create();
        var user = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(company, user);

        ActAs(user.Id, company.Id);
        return (company.Id, user.Id);
    }
}
