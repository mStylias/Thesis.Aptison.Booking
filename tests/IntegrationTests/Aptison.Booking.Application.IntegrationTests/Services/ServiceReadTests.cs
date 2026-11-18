using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Services;

/// <summary>
/// The drawer's two reads. Categories are optional-owner, so which platform ones a shop sees is the
/// business-type test in SQL; the counts go through soft-delete and ownership filters.
/// </summary>
public class ServiceReadTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task GetServiceOptions_WhenTheShopIsABarbershop_ShouldListItsCategoriesLanguagesAndStaff()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new GetServiceOptionsRequest { CompanyId = shop.CompanyId, LanguageCode = "en" });

        // Assert
        var options = result.Data!;
        await Assert.That(options.Currency).IsEqualTo("EUR");
        await Assert.That(options.Languages).IsEquivalentTo(["el", "en"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        // Platform categories list before the shop's own. The hair-salon category sorts first but is
        // another vertical's, and the other shop's is hidden.
        await Assert.That(options.Categories.Select(category => category.Id)).IsEquivalentTo(
            [
                shop.HaircutsCategoryId, TestIds.ServiceCategories.Haircuts, TestIds.ServiceCategories.BeardAndShave,
                TestIds.ServiceCategories.Grooming, TestIds.ServiceCategories.Colour, TestIds.ServiceCategories.Kids,
                shop.BeardsCategoryId
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var haircuts = options.Categories[0];
        await Assert.That(haircuts.Name).IsEqualTo("Haircuts");
        await Assert.That(haircuts.IsOwned).IsFalse();
        await Assert.That(haircuts.ServiceCount).IsEqualTo(1).Because("the other shop's services are not counted");
        await Assert.That(haircuts.Translations).IsEquivalentTo(
            [new ServiceCategoryTranslationViewDto("el", "Κουρέματα"), new ServiceCategoryTranslationViewDto("en", "Haircuts")],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(options.Categories[^1].IsOwned).IsTrue();

        await Assert.That(options.StaffMembers).IsEquivalentTo(
            [
                new ServiceOptionStaffMemberViewDto(shop.EleniId, "Eleni R.", true, false),
                new ServiceOptionStaffMemberViewDto(shop.MariaId, "Maria K.", false, true),
                new ServiceOptionStaffMemberViewDto(shop.NikosId, "Nikos B.", true, true)
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetService_WhenTheServiceWasBooked_ShouldReturnBothLanguagesAndForbidDelete()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new GetServiceRequest { ServiceId = shop.HaircutId });

        // Assert
        var service = result.Data!;
        await Assert.That(service.CategoryId).IsEqualTo(shop.HaircutsCategoryId);
        await Assert.That(service.Translations).IsEquivalentTo(
            [
                new ServiceTranslationViewDto("el", "Κούρεμα", "Με ψαλίδι"),
                new ServiceTranslationViewDto("en", "Haircut", "With scissors")
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(service.Staff).IsEquivalentTo(
            [new ServiceStaffViewDto(shop.MariaId, 30m, 40), new ServiceStaffViewDto(shop.NikosId, 25m, null)],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(service.TimesBooked).IsEqualTo(1);
        await Assert.That(service.CanDelete).IsFalse();
    }

    [Test]
    public async Task GetService_WhenNobodyBookedIt_ShouldAllowDelete()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new GetServiceRequest { ServiceId = shop.ShaveId });

        // Assert
        await Assert.That(result.Data!.CanDelete).IsTrue();
        await Assert.That(result.Data.TimesBooked).IsEqualTo(0);
    }

    [Test]
    public async Task GetService_WhenTheServiceIsAnotherShops_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);
        var otherShop = await ServiceCatalogSeeder.SeedAsync(this);
        ActAsCompany(otherShop.CompanyId);

        // Act
        var result = await Sender.SendAsync(new GetServiceRequest { ServiceId = shop.HaircutId });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Services.NotFound);
    }
}
