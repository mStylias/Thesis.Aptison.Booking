using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// The grid's Services filter. Services are ownership-scoped, so what keeps two shops' lists apart
/// is the filter in SQL; the folded name is a generated column only the database fills.
/// </summary>
public class ListServiceOptionsODataTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    /// <summary>The endpoint's options.</summary>
    private static readonly SynaxODataOptions ODataOptions =
        new SynaxODataOptions().Searchable<ServiceOptionODataResponse>(service => service.NameNormalized);

    [Test]
    public async Task ListServices_WhenAServiceIsInactive_ShouldStillListItButNoOtherShopsServices()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        var retired = ServiceFactory.Create(shop.CompanyId, isActive: false);
        await SeedAsync(retired);
        await SeedAsync(ServiceFactory.CreateTranslation(retired.Id, "Ξύρισμα"));
        var otherShop = CompanyFactory.Create();
        ActAsNobody();
        await SeedAsync(otherShop);
        ActAsCompany(otherShop.Id);
        await SeedAsync(ServiceFactory.Create(otherShop.Id));
        ActAsCompany(shop.CompanyId);

        // Act
        var page = await RunAsync("?$count=true");

        // Assert
        var rows = page.Value.Cast<ServiceOptionODataResponse>().ToList();
        await Assert.That(rows.Select(row => row.Id)).IsEquivalentTo([shop.HaircutId, shop.BeardTrimId, retired.Id]);
        await Assert.That(rows.Single(row => row.Id == retired.Id).IsActive).IsFalse();
        await Assert.That(rows.Single(row => row.Id == shop.HaircutId).CategoryId).IsEqualTo(shop.HaircutsCategoryId);
        await Assert.That(page.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ListServices_WhenTheLanguageIsEnglish_ShouldNameInEnglishOrFallBack()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("", languageCode: "en");

        // Assert
        var namesById = page.Value.Cast<ServiceOptionODataResponse>().ToDictionary(row => row.Id, row => row.Name);
        await Assert.That(namesById[shop.HaircutId]).IsEqualTo("Haircut");
        // The beard trim has only a Greek name.
        await Assert.That(namesById[shop.BeardTrimId]).IsEqualTo("Περιποίηση γενειάδας");
    }

    [Test]
    [Arguments("κουρεμα")]
    [Arguments("ΚΟΎΡΕΜΑ")]
    public async Task ListServices_WhenSearchingAFoldedTerm_ShouldMatchTheAccentedName(string term)
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$search={Uri.EscapeDataString(term)}");

        // Assert
        await Assert.That(page.Value.Cast<ServiceOptionODataResponse>().Select(row => row.Id)).IsEquivalentTo([shop.HaircutId]);
        await Assert.That(GetLastSqlMentioning("FROM services")).Contains("name_normalized");
    }

    [Test]
    public async Task ListServices_WhenSerializingARow_ShouldLeaveTheSearchTargetOut()
    {
        // Arrange
        await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("?$top=1");

        // Assert
        var row = ODataQueryRunner.ToJson(page).GetProperty("value")[0];
        await Assert.That(row.TryGetProperty("nameNormalized", out _)).IsFalse();
        await Assert.That(row.TryGetProperty("isActive", out _)).IsTrue();
    }

    private async Task<ODataPage> RunAsync(string queryString, string languageCode = "el")
    {
        var result = await Sender.SendAsync(new ListServiceOptionsODataRequest { LanguageCode = languageCode });
        return await ODataQueryRunner.RunAsync(result.Data!, queryString, ODataOptions, Services);
    }
}
