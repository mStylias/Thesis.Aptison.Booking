using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// The calendar's customer filter. An account holder books at many shops, so what keeps two shops'
/// customer lists apart is the ownership filter on the appointments subquery — which only SQL can prove.
/// </summary>
public class ListCustomerOptionsODataTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    /// <summary>The endpoint's options.</summary>
    private static readonly SynaxODataOptions ODataOptions = new SynaxODataOptions
    {
        AllowedOrderByProperties = { "displayName", "lastAppointmentDate" }
    }.Searchable<CustomerOptionODataResponse>(
        customer => customer.NameNormalized, customer => customer.Email, customer => customer.PhoneNumber);

    [Test]
    public async Task ListCustomers_WhenTheShopHasBookings_ShouldListOnlyItsOwnCustomers()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("?$orderby=displayName&$count=true");

        // Assert
        var rows = page.Value.Cast<CustomerOptionODataResponse>().ToList();
        await Assert.That(rows.Select(row => row.Id)).IsEquivalentTo(
            [shop.GiorgosId, shop.EleniId], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(page.Count).IsEqualTo(2);
        await Assert.That(rows[0].DisplayName).IsEqualTo("Γιώργος Παπαδόπουλος");
    }

    [Test]
    public async Task ListCustomers_WhenACustomerHasSeveralBookings_ShouldReportTheLatestDayInAnyStatus()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=id in ({shop.EleniId})");

        // Assert
        var row = (CustomerOptionODataResponse)page.Value.Single()!;
        await Assert.That(row.LastAppointmentDate).IsEqualTo(new DateOnly(2026, 9, 29));
    }

    [Test]
    public async Task ListCustomers_WhenSearchingAFoldedTerm_ShouldMatchAnAccentedName()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$search={Uri.EscapeDataString("γιωργ")}");

        // Assert
        // 'Γιωργής' matches the term too, but booked only at the other shop.
        var ids = page.Value.Cast<CustomerOptionODataResponse>().Select(row => row.Id);
        await Assert.That(ids).IsEquivalentTo([shop.GiorgosId]);
    }

    [Test]
    public async Task ListCustomers_WhenSearchingAnAccentedCapitalisedTerm_ShouldFoldItBeforeMatching()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$search={Uri.EscapeDataString("ΓΙΏΡΓ")}");

        // Assert
        var ids = page.Value.Cast<CustomerOptionODataResponse>().Select(row => row.Id);
        await Assert.That(ids).IsEquivalentTo([shop.GiorgosId]);
    }

    [Test]
    public async Task ListCustomers_WhenSerialized_ShouldNotExposeTheSearchColumn()
    {
        // Arrange
        await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("?$top=1");

        // Assert
        var row = ODataQueryRunner.ToJson(page).GetProperty("value")[0];
        await Assert.That(row.TryGetProperty("nameNormalized", out _)).IsFalse();
        await Assert.That(row.GetProperty("lastAppointmentDate").GetString()).IsNotNull();
    }

    [Test]
    public async Task ListCustomers_WhenSearchingPartOfAnEmail_ShouldMatchItsOwner()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await SetContactAsync(shop.EleniId, email: "eleni.nikolaou@example.test", phoneNumber: null);

        // Act
        // Quoted: an unquoted OData search word takes letters only.
        var page = await RunAsync($"?$search={Uri.EscapeDataString("\"NIKOLAOU@\"")}");

        // Assert
        var row = page.Value.Cast<CustomerOptionODataResponse>().Single();
        await Assert.That(row.Id).IsEqualTo(shop.EleniId);
        await Assert.That(row.Email).IsEqualTo("eleni.nikolaou@example.test");
    }

    [Test]
    public async Task ListCustomers_WhenSearchingPhoneDigits_ShouldMatchTheStoredNumber()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await SetContactAsync(shop.EleniId, email: null, phoneNumber: "+306977123456");

        // Act
        var page = await RunAsync($"?$search={Uri.EscapeDataString("\"6977123\"")}");

        // Assert
        var row = page.Value.Cast<CustomerOptionODataResponse>().Single();
        await Assert.That(row.Id).IsEqualTo(shop.EleniId);
        await Assert.That(row.PhoneNumber).IsEqualTo("+306977123456");
        await Assert.That(row.HasAccount).IsTrue().Because("an account no one has signed in to yet is still the person's own");
    }

    [Test]
    public async Task ListCustomers_WhenAQuotedPhraseHoldsAnEscapedQuote_ShouldMatchTheQuoteLiterally()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await DbContext.Set<User>()
            .Where(user => user.Id == shop.EleniId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.FirstName, "Ελένη \"Λένα\""));

        // Act
        var page = await RunAsync($"?$search={Uri.EscapeDataString("\"\\\"λενα\\\"\"")}");

        // Assert
        await Assert.That(page.Value.Cast<CustomerOptionODataResponse>().Select(row => row.Id)).IsEquivalentTo([shop.EleniId]);
    }

    private async Task SetContactAsync(Guid userId, string? email, string? phoneNumber)
    {
        await DbContext.Set<User>()
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.Email, email ?? $"user-{userId:N}@example.test")
                .SetProperty(user => user.PhoneNumber, phoneNumber ?? "+306900000000"));
    }

    private async Task<ODataPage> RunAsync(string queryString)
    {
        var result = await Sender.SendAsync(new ListCustomerOptionsODataRequest());
        return await ODataQueryRunner.RunAsync(result.Data!, queryString, ODataOptions, Services);
    }
}
