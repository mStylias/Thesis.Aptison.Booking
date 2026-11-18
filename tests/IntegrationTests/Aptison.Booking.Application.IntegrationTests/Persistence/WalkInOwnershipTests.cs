using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aptison.Booking.Application.IntegrationTests.Persistence;

/// <summary>A walk-in belongs to the shop that typed it in: the ownership filter on <c>users</c> and the email guard trigger.</summary>
public class WalkInOwnershipTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task QueryUsers_WhenAnotherShopIsInScope_ShouldHideItsWalkInsButNotAccounts()
    {
        // Arrange
        var shopA = CompanyFactory.Create();
        var shopB = CompanyFactory.Create();
        var walkIn = UserFactory.Create(ownerId: shopA.Id);
        var account = UserFactory.Create(keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(shopA, shopB);
        await SeedAsync(walkIn, account);

        // Act
        ActAsCompany(shopB.Id);
        var visibleToB = await DbContext.Set<User>().Select(user => user.Id).ToListAsync();
        ActAsCompany(shopA.Id);
        var visibleToA = await DbContext.Set<User>().Select(user => user.Id).ToListAsync();

        // Assert
        await Assert.That(visibleToB).Contains(account.Id);
        await Assert.That(visibleToB).DoesNotContain(walkIn.Id);
        await Assert.That(visibleToA).Contains(walkIn.Id);
    }

    [Test]
    public async Task InsertWalkIn_WhenAnAccountHoldsTheEmail_ShouldViolateTheGuard()
    {
        // Arrange
        var shop = CompanyFactory.Create();
        var account = UserFactory.Create(email: "eleni@example.test", keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(shop);
        await SeedAsync(account);

        // Act
        var exception = await Assert.That(() => SeedAsync(UserFactory.Create(email: "ELENI@example.test", ownerId: shop.Id)))
            .Throws<DbUpdateException>();

        // Assert
        var postgres = exception!.InnerException as PostgresException;
        await Assert.That(postgres?.ConstraintName).IsEqualTo("ck_users_walk_in_email_not_account");
    }
}
