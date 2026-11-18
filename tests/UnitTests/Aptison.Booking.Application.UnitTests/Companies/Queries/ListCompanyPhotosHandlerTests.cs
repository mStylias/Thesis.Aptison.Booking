using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;
using TUnit.Assertions.Enums;

namespace Aptison.Booking.Application.UnitTests.Companies.Queries;

public class ListCompanyPhotosHandlerTests
{
    [Test]
    public async Task HandleListCompanyPhotos_WhenTheShopHasPhotos_ShouldListThemInDisplayOrder()
    {
        // Arrange
        var world = new CompanyWorld();
        var second = world.SeedPhoto(1);
        var first = world.SeedPhoto(0);
        world.UnitOfWork.Seed(CompanyFactory.CreatePhotoTranslation(first.Id, "Η πρόσοψη", TestIds.Languages.Greek));

        // Act
        var result = await HandleAsync(world, world.CompanyId);

        // Assert
        await Assert.That(result.Data!.Select(photo => photo.Id))
            .IsEquivalentTo([first.Id, second.Id], CollectionOrdering.Matching);
        await Assert.That(result.Data![0].Url).IsEqualTo($"{CompanyWorld.PublicBaseUrl}/{first.StorageKey}");
        await Assert.That(result.Data[0].AltTexts).IsEquivalentTo([new CompanyPhotoAltTextViewDto("el", "Η πρόσοψη")]);
    }

    [Test]
    public async Task HandleListCompanyPhotos_WhenTheCallerHasNoCompany_ShouldRefuseAsScopeMissing()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, companyId: null);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Authorization.CompanyScopeMissing);
    }

    private static Task<Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>> HandleAsync(CompanyWorld world, Guid? companyId) =>
        new ListCompanyPhotosHandler(world.UnitOfWork, world.FileStorageOptions)
            .ValidateAndHandleAsync(new ListCompanyPhotosRequest { CompanyId = companyId });
}
