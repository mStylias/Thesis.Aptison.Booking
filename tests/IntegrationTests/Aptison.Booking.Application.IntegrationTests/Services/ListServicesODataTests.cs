using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.ResponseOData;
using Aptison.Booking.Application.Features.Services.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Services;

/// <summary>
/// The services grid. What a fake cannot answer: whether every aggregate translates, whether the
/// soft-delete and ownership filters reach the nested staff and booking counts, and whether each
/// filter the grid sends composes over the projection.
/// </summary>
public class ListServicesODataTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    /// <summary>The endpoint's options.</summary>
    private static readonly SynaxODataOptions ODataOptions =
        new SynaxODataOptions().Searchable<ServiceODataResponse>(service => service.NameNormalized);

    [Test]
    public async Task ListServices_WhenReadInGreek_ShouldFillEveryMemberOfTheRow()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var rows = await ReadRowsAsync("?$count=true");

        // Assert
        await Assert.That(rows.Select(row => row.Id)).IsEquivalentTo([shop.HaircutId, shop.BeardTrimId, shop.ShaveId]);

        var haircut = rows.Single(row => row.Id == shop.HaircutId);
        await Assert.That(haircut.Name).IsEqualTo("Κούρεμα");
        await Assert.That(haircut.Description).IsEqualTo("Με ψαλίδι");
        await Assert.That(haircut.CategoryId).IsEqualTo(shop.HaircutsCategoryId);
        await Assert.That(haircut.CategoryName).IsEqualTo("Κουρέματα");
        await Assert.That(haircut.Price).IsEqualTo(20m);
        await Assert.That(haircut.Currency).IsEqualTo("EUR");
        await Assert.That(haircut.DurationMinutes).IsEqualTo(30);
        await Assert.That(haircut.BufferBeforeMinutes).IsEqualTo(0);
        await Assert.That(haircut.BufferAfterMinutes).IsEqualTo(5);
        await Assert.That(haircut.CalendarMinutes).IsEqualTo(35);
        await Assert.That(haircut.IsActive).IsTrue();
        // Kostas is a deleted staff member: his live assignment must not surface.
        await Assert.That(haircut.Staff).IsEquivalentTo(
            [
                new ServiceStaffODataViewDto
                {
                    StaffMemberId = shop.MariaId, DisplayName = "Maria K.", IsActive = false,
                    CustomPrice = 30m, CustomDurationMinutes = 40
                },
                new ServiceStaffODataViewDto
                {
                    StaffMemberId = shop.NikosId, DisplayName = "Nikos B.", IsActive = true, CustomPrice = 25m
                }
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(haircut.StaffCount).IsEqualTo(1);
        await Assert.That(haircut.MinPrice).IsEqualTo(25m);
        await Assert.That(haircut.MaxPrice).IsEqualTo(25m);
        await Assert.That(haircut.IsBookableOnline).IsTrue();
        await Assert.That(haircut.TimesBooked).IsEqualTo(1).Because("the later cancelled booking does not count");
        await Assert.That(haircut.LastBookedAt).IsEqualTo(new DateTimeOffset(shop.HaircutLastBookedAt));
        await Assert.That(haircut.CreatedAt.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(haircut.UpdatedAt).IsNull();

        var beardTrim = rows.Single(row => row.Id == shop.BeardTrimId);
        await Assert.That(beardTrim.CategoryName).IsEqualTo("Γένια");
        await Assert.That(beardTrim.StaffCount).IsEqualTo(1);
        await Assert.That(beardTrim.MinPrice).IsEqualTo(10m).Because("Eleni has no own price");
        await Assert.That(beardTrim.IsBookableOnline).IsFalse();
        await Assert.That(beardTrim.TimesBooked).IsEqualTo(0);
        await Assert.That(beardTrim.LastBookedAt).IsNull();

        var shave = rows.Single(row => row.Id == shop.ShaveId);
        await Assert.That(shave.CategoryId).IsNull();
        await Assert.That(shave.CategoryName).IsNull();
        await Assert.That(shave.IsBookableOnline).IsFalse().Because("only a non-bookable staff member offers it");
    }

    [Test]
    [Arguments("en", "Haircut", "Haircuts", "With scissors")]
    [Arguments("fr", "Haircut", "Haircuts", "With scissors")]
    public async Task ListServices_WhenReadInAnotherLanguage_ShouldNameInItOrFallBackByLanguageId(
        string languageCode, string haircutName, string haircutCategoryName, string haircutDescription)
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var rows = await ReadRowsAsync("", languageCode);

        // Assert
        var haircut = rows.Single(row => row.Id == shop.HaircutId);
        await Assert.That(haircut.Name).IsEqualTo(haircutName);
        await Assert.That(haircut.CategoryName).IsEqualTo(haircutCategoryName);
        await Assert.That(haircut.Description).IsEqualTo(haircutDescription);
        // The beard trim and its category have only a Greek name, or a Greek and an English one.
        var beardTrim = rows.Single(row => row.Id == shop.BeardTrimId);
        await Assert.That(beardTrim.Name).IsEqualTo("Περιποίηση γενειάδας");
        await Assert.That(beardTrim.CategoryName).IsEqualTo("Beards");
    }

    [Test]
    [Arguments("contains(nameNormalized,'κουρ')", "haircut")]
    [Arguments("contains(description,'ψαλ')", "haircut")]
    [Arguments("categoryId eq null", "shave")]
    [Arguments("isActive eq true", "haircut,shave")]
    [Arguments("isBookableOnline eq false", "beard,shave")]
    [Arguments("price ge 12", "haircut,shave")]
    [Arguments("durationMinutes le 20", "beard,shave")]
    [Arguments("bufferBeforeMinutes eq 5", "beard")]
    [Arguments("bufferAfterMinutes eq 0", "shave")]
    [Arguments("calendarMinutes eq 35", "haircut")]
    [Arguments("timesBooked ge 1", "haircut")]
    [Arguments("staffCount eq 1 and minPrice lt 20", "beard,shave")]
    [Arguments("lastBookedAt ge 2026-09-22T00:00:00Z and lastBookedAt lt 2026-09-23T00:00:00Z", "haircut")]
    [Arguments("createdAt lt 2100-01-01T00:00:00Z and updatedAt eq null", "haircut,beard,shave")]
    public async Task ListServices_WhenFilteringAColumn_ShouldComposeOverTheRow(string filter, string expectedKeys)
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);
        var idsByKeys = new Dictionary<string, Guid>
        {
            ["haircut"] = shop.HaircutId, ["beard"] = shop.BeardTrimId, ["shave"] = shop.ShaveId
        };

        // Act
        var rows = await ReadRowsAsync($"?$filter={Uri.EscapeDataString(filter)}");

        // Assert
        await Assert.That(rows.Select(row => row.Id))
            .IsEquivalentTo(expectedKeys.Split(',').Select(key => idsByKeys[key]));
    }

    [Test]
    public async Task ListServices_WhenFilteringByCategoryOrBarber_ShouldMatchOnTheIds()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var byCategory = await ReadRowsAsync(
            $"?$filter={Uri.EscapeDataString($"categoryId in ({shop.HaircutsCategoryId},{shop.BeardsCategoryId})")}");
        var byBarber = await ReadRowsAsync(
            $"?$filter={Uri.EscapeDataString($"staff/any(s: s/staffMemberId in ({shop.EleniId}))")}");
        var byDeletedBarber = await ReadRowsAsync(
            $"?$filter={Uri.EscapeDataString($"staff/any(s: s/staffMemberId in ({shop.KostasId}))")}");

        // Assert
        await Assert.That(byCategory.Select(row => row.Id)).IsEquivalentTo([shop.HaircutId, shop.BeardTrimId]);
        await Assert.That(byBarber.Select(row => row.Id)).IsEquivalentTo([shop.BeardTrimId, shop.ShaveId]);
        await Assert.That(byDeletedBarber).IsEmpty();
    }

    [Test]
    [Arguments("categoryName")]
    [Arguments("maxPrice desc")]
    [Arguments("lastBookedAt desc")]
    public async Task ListServices_WhenSortingAComputedColumn_ShouldOrderInSql(string orderBy)
    {
        // Arrange
        await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var rows = await ReadRowsAsync($"?$orderby={Uri.EscapeDataString(orderBy)}");

        // Assert
        await Assert.That(rows).Count().IsEqualTo(3);
    }

    [Test]
    public async Task ListServices_WhenSerializingARow_ShouldLeaveTheSearchTargetOut()
    {
        // Arrange
        await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("?$top=1");

        // Assert
        var row = ODataQueryRunner.ToJson(page).GetProperty("value")[0];
        await Assert.That(row.TryGetProperty("nameNormalized", out _)).IsFalse();
        await Assert.That(row.TryGetProperty("isBookableOnline", out _)).IsTrue();
    }

    private async Task<List<ServiceODataResponse>> ReadRowsAsync(string queryString, string languageCode = "el") =>
        (await RunAsync(queryString, languageCode)).Value.Cast<ServiceODataResponse>().ToList();

    private async Task<ODataPage> RunAsync(string queryString, string languageCode = "el")
    {
        var result = await Sender.SendAsync(new ListServicesODataRequest { LanguageCode = languageCode });
        return await ODataQueryRunner.RunAsync(result.Data!, queryString, ODataOptions, Services);
    }
}
