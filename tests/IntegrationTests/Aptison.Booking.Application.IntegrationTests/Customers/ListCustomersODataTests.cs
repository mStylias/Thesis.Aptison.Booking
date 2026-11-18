using Aptison.Booking.Application.Features.Customers.Common.ResponseOData;
using Aptison.Booking.Application.Features.Customers.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Customers;

/// <summary>Every column is a correlated subquery, so only SQL can prove it translates and counts right.</summary>
public class ListCustomersODataTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private static readonly SynaxODataOptions ODataOptions = new SynaxODataOptions()
        .Searchable<CustomerODataResponse>(
            customer => customer.NameNormalized, customer => customer.Email, customer => customer.PhoneNumber);

    [Test]
    public async Task ListCustomers_WhenACustomerHasHistory_ShouldCountItByStatus()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var eleni = await GetRowAsync(shop, shop.EleniId);

        // Assert
        await Assert.That(eleni.VisitCount).IsEqualTo(2);
        await Assert.That(eleni.UpcomingCount).IsEqualTo(1);
        await Assert.That(eleni.NoShowCount).IsEqualTo(1);
        await Assert.That(eleni.CancellationCount).IsEqualTo(1);
        await Assert.That(eleni.NextAppointmentAt).IsNotNull();
        await Assert.That(eleni.LastVisitDate!.Value).IsGreaterThan(eleni.FirstVisitDate!.Value);
        await Assert.That(eleni.DaysSinceLastVisit!.Value).IsBetween(9, 11).Because("the last visit was 10 days ago, give or take the shop's midnight");
        await Assert.That(eleni.CustomerSince).IsEqualTo(eleni.FirstVisitDate!.Value);
        await Assert.That(eleni.IsWalkIn).IsFalse();
    }

    [Test]
    public async Task ListCustomers_WhenTheShopKeepsData_ShouldShowItsPhoneNoteBarberAndBlock()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var eleni = await GetRowAsync(shop, shop.EleniId);

        // Assert
        await Assert.That(eleni.PhoneNumber).IsEqualTo("+306911111111").Because("she has set no phone of her own");
        await Assert.That(eleni.HasNote).IsTrue();
        await Assert.That(eleni.IsBlocked).IsTrue();
        await Assert.That(eleni.PreferredStaffMemberId).IsEqualTo(shop.BarberId);
        await Assert.That(eleni.PreferredStaffDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(eleni.PreferredStaffIsActive!.Value).IsTrue();
    }

    [Test]
    public async Task ListCustomers_WhenTheCallerMayNotReadAnalytics_ShouldLeaveSpendNull()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var eleni = await GetRowAsync(shop, shop.EleniId);

        // Assert
        await Assert.That(eleni.TotalSpend).IsNull();
        await Assert.That(eleni.AverageSpend).IsNull();
    }

    [Test]
    public async Task ListCustomers_WhenTheCallerMayReadAnalytics_ShouldSumCompletedVisitsOnly()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        Services.GetRequiredService<FakeCurrentPermissions>().Grant(PermissionCodes.Analytics.Read);

        // Act
        var eleni = await GetRowAsync(shop, shop.EleniId);

        // Assert
        await Assert.That(eleni.TotalSpend).IsEqualTo(50m);
        await Assert.That(eleni.AverageSpend).IsEqualTo(25m);
    }

    [Test]
    public async Task ListCustomers_WhenAnotherShopHasAWalkIn_ShouldListOnlyThisShopsCustomers()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync(shop, "?$orderby=displayName&$count=true");

        // Assert
        var ids = page.Value.Cast<CustomerODataResponse>().Select(row => row.Id);
        await Assert.That(ids).IsEquivalentTo([shop.EleniId, shop.KostasId], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(page.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ListCustomers_WhenFilteringNotBackForDays_ShouldReturnOnlyLapsedCustomers()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync(shop, "?$filter=daysSinceLastVisit ge 30 and upcomingCount eq 0");

        // Assert
        var ids = page.Value.Cast<CustomerODataResponse>().Select(row => row.Id);
        await Assert.That(ids).IsEquivalentTo([shop.KostasId]);
    }

    [Test]
    public async Task ListCustomers_WhenSortingBySpendWithoutAnalytics_ShouldRefuse()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new ListCustomersODataRequest { CompanyId = shop.CompanyId, QueriesSpend = true });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(Domain.Errors.ErrorCodes.Customers.SpendForbidden);
    }

    private async Task<CustomerODataResponse> GetRowAsync(CustomerShop shop, Guid customerId)
    {
        var page = await RunAsync(shop, $"?$filter=id eq {customerId}");
        return page.Value.Cast<CustomerODataResponse>().Single();
    }

    private async Task<ODataPage> RunAsync(CustomerShop shop, string queryString)
    {
        var result = await Sender.SendAsync(new ListCustomersODataRequest { CompanyId = shop.CompanyId });
        return await ODataQueryRunner.RunAsync(result.Data!, queryString, ODataOptions, Services);
    }
}
