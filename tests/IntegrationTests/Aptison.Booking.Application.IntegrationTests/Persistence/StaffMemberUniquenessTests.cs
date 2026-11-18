using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Infrastructure.Persistence;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Persistence;

/// <summary>A user is active staff at one company at most, which is what lets the API resolve the company from the user.</summary>
public class StaffMemberUniquenessTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private const string UniqueUserIndex = "ux_staff_members_user";

    [Test]
    public async Task InsertStaffMember_WhenTheUserIsActiveStaffAtAnotherCompany_ShouldViolateTheUniqueIndex()
    {
        // Arrange
        var (firstCompanyId, secondCompanyId, userId) = await SeedTwoCompaniesAndUserAsync();

        ActAsCompany(firstCompanyId);
        await SeedAsync(StaffFactory.Create(firstCompanyId, userId));

        ActAsCompany(secondCompanyId);

        // Act
        var exception = await Assert.That(() => SeedAsync(StaffFactory.Create(secondCompanyId, userId)))
            .Throws<DbUpdateException>();

        // Assert
        await Assert.That(new PostgresConstraintReader().IsViolated(exception!, UniqueUserIndex)).IsTrue();
    }

    [Test]
    public async Task InsertStaffMember_WhenTheUserIsInactiveStaffAtAnotherCompany_ShouldSucceed()
    {
        // Arrange
        var (firstCompanyId, secondCompanyId, userId) = await SeedTwoCompaniesAndUserAsync();

        ActAsCompany(firstCompanyId);
        await SeedAsync(StaffFactory.Create(firstCompanyId, userId, isActive: false));

        ActAsCompany(secondCompanyId);
        var newRow = StaffFactory.Create(secondCompanyId, userId);

        // Act
        await SeedAsync(newRow);

        // Assert
        await Assert.That(await FindAsync<StaffMember>(newRow.Id)).IsNotNull();
    }

    [Test]
    public async Task InsertStaffMember_WhenTheUserLeftAnotherCompany_ShouldSucceed()
    {
        // Arrange
        var (firstCompanyId, secondCompanyId, userId) = await SeedTwoCompaniesAndUserAsync();
        var formerRow = StaffFactory.Create(firstCompanyId, userId);

        ActAsCompany(firstCompanyId);
        await SeedAsync(formerRow);

        UnitOfWork.GetRepository<StaffMember>().Remove(
            await DbContext.Set<StaffMember>().FirstAsync(row => row.Id == formerRow.Id));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        ActAsCompany(secondCompanyId);
        var newRow = StaffFactory.Create(secondCompanyId, userId);

        // Act
        await SeedAsync(newRow);

        // Assert
        await Assert.That(await FindAsync<StaffMember>(newRow.Id)).IsNotNull();
    }

    private async Task<(Guid FirstCompanyId, Guid SecondCompanyId, Guid UserId)> SeedTwoCompaniesAndUserAsync()
    {
        var first = CompanyFactory.Create();
        var second = CompanyFactory.Create();
        var user = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(first, second, user);

        return (first.Id, second.Id, user.Id);
    }
}
