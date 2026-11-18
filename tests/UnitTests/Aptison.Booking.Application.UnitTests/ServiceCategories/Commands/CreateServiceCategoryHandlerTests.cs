using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.ServiceCategories.Commands;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Application.UnitTests.Services;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.ServiceCategories.Commands;

public class CreateServiceCategoryHandlerTests
{
    [Test]
    public async Task HandleCreateServiceCategory_WhenTheBodyIsValid_ShouldOwnItAndSortItAfterEveryVisibleCategory()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        world.SeedCategory("Κουρέματα", "Haircuts", sortOrder: 30);
        world.SeedCategory("Παιδικά", "Kids", companyId: world.CompanyId, sortOrder: 40);
        world.SeedCategory("Βαφή", "Colour", businessTypeId: TestIds.BusinessTypes.HairSalon, sortOrder: 90);
        world.SeedCategory("Άλλη", "Other", companyId: Guid.CreateVersion7(), sortOrder: 100);

        // Act
        var result = await HandleAsync(world, BuildRequest(world, "Γένια", "Beards"));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var created = world.UnitOfWork.Entities<ServiceCategory>().Single(category => category.Id == result.Data!.Id);
        await Assert.That(created.OwnerId).IsEqualTo(world.CompanyId);
        await Assert.That(created.BusinessTypeId).IsNull();
        await Assert.That(created.SortOrder).IsEqualTo(50);
        await Assert.That(world.UnitOfWork.Entities<ServiceCategoryTranslation>()
                .Where(translation => translation.ServiceCategoryId == created.Id)
                .Select(translation => translation.Name))
            .IsEquivalentTo(["Γένια", "Beards"]);
        await Assert.That(world.Outbox.Enqueued.Cast<CompanyCalendarChanged>()).IsEquivalentTo(
            [new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleCreateServiceCategory_WhenAPlatformCategoryHasTheFoldedName_ShouldRefuseAsConflict()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        world.SeedCategory("Κουρέματα", "Haircuts");

        // Act
        var result = await HandleAsync(world, BuildRequest(world, "Γένια", " HAIRCUTS"));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.ServiceCategories.NameTaken);
        await Assert.That(result.Error.ValidationErrors!.Select(error => error.PropertyName!))
            .IsEquivalentTo(["Translations[1].Name"]);
    }

    [Test]
    public async Task HandleCreateServiceCategory_WhenANameIsTooLongOrALanguageMissing_ShouldRefuseBoth()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = BuildRequest(world, new string('α', 101), "Beards");
        request.Translations = [request.Translations[0]];

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Select(error => (error.Code, error.PropertyName)))
            .IsEquivalentTo(
            [
                (ErrorCodes.ServiceCategories.MissingTranslation, (string?)"Translations"),
                (ErrorCodes.ServiceCategories.InvalidName, "Translations[0].Name")
            ]);
    }

    private static CreateServiceCategoryRequest BuildRequest(ServiceCatalogWorld world, string greekName, string englishName) =>
        new()
        {
            CompanyId = world.CompanyId,
            Translations =
            [
                new ServiceCategoryTranslationAddEditDto { LanguageCode = "el", Name = greekName },
                new ServiceCategoryTranslationAddEditDto { LanguageCode = "en", Name = englishName }
            ]
        };

    private static Task<Result<ServiceCategoryResponse, HttpError>> HandleAsync(
        ServiceCatalogWorld world, CreateServiceCategoryRequest request) =>
        new CreateServiceCategoryHandler(world.UnitOfWork, world.Outbox).ValidateAndHandleAsync(request);
}
