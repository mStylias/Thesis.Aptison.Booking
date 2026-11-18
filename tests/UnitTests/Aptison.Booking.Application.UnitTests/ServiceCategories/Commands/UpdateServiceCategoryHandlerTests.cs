using Aptison.Booking.Application.Features.ServiceCategories.Commands;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Application.UnitTests.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.ServiceCategories.Commands;

public class UpdateServiceCategoryHandlerTests
{
    [Test]
    public async Task HandleUpdateServiceCategory_WhenTheShopOwnsIt_ShouldRenameBothLanguages()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var categoryId = world.SeedCategory("Γένια", "Beards", companyId: world.CompanyId);

        // Act
        var result = await HandleAsync(world, BuildRequest(world, categoryId, "Περιποίηση", "Grooming"));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<ServiceCategoryTranslation>().Select(translation => translation.Name))
            .IsEquivalentTo(["Περιποίηση", "Grooming"]);
        await Assert.That(world.Outbox.Enqueued).Count().IsEqualTo(1);
    }

    [Test]
    public async Task HandleUpdateServiceCategory_WhenItIsAPlatformCategory_ShouldRefuseAsNotOwned()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var categoryId = world.SeedCategory("Κουρέματα", "Haircuts");

        // Act
        var result = await HandleAsync(world, BuildRequest(world, categoryId, "Κούρεμα", "Cuts"));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.ServiceCategories.NotOwned);
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    [Test]
    public async Task HandleUpdateServiceCategory_WhenItIsAnotherShops_ShouldReturnNotFound()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var categoryId = world.SeedCategory("Άλλη", "Other", companyId: Guid.CreateVersion7());

        // Act
        var result = await HandleAsync(world, BuildRequest(world, categoryId, "Νέα", "New"));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.ServiceCategories.NotFound);
    }

    private static UpdateServiceCategoryRequest BuildRequest(
        ServiceCatalogWorld world, Guid categoryId, string greekName, string englishName) =>
        new()
        {
            CompanyId = world.CompanyId,
            CategoryId = categoryId,
            Translations =
            [
                new ServiceCategoryTranslationAddEditDto { LanguageCode = "el", Name = greekName },
                new ServiceCategoryTranslationAddEditDto { LanguageCode = "en", Name = englishName }
            ]
        };

    private static Task<Result<ServiceCategoryResponse, HttpError>> HandleAsync(
        ServiceCatalogWorld world, UpdateServiceCategoryRequest request) =>
        new UpdateServiceCategoryHandler(world.UnitOfWork, world.Outbox).ValidateAndHandleAsync(request);
}
