using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class UpdateCompanyPhotoHandlerTests
{
    [Test]
    public async Task HandleUpdateCompanyPhoto_WhenALanguageHasNoAltText_ShouldAddIt()
    {
        // Arrange
        var world = new CompanyWorld();
        var photo = world.SeedPhoto(0);

        // Act
        var result = await HandleAsync(world, photo.Id, AltText("en", "  The chairs  "));

        // Assert
        await Assert.That(result.Data!.AltTexts).IsEquivalentTo([new CompanyPhotoAltTextViewDto("en", "The chairs")]);
    }

    [Test]
    public async Task HandleUpdateCompanyPhoto_WhenOneLanguageIsSetAndAnotherCleared_ShouldLeaveOthersUntouched()
    {
        // Arrange
        var world = new CompanyWorld();
        var photo = world.SeedPhoto(0);
        world.UnitOfWork.Seed(
            CompanyFactory.CreatePhotoTranslation(photo.Id, "Η πρόσοψη", TestIds.Languages.Greek),
            CompanyFactory.CreatePhotoTranslation(photo.Id, "The front", TestIds.Languages.English));
        var otherPhoto = world.SeedPhoto(1);
        world.UnitOfWork.Seed(CompanyFactory.CreatePhotoTranslation(otherPhoto.Id, "Άλλη", TestIds.Languages.Greek));

        // Act
        var result = await HandleAsync(world, photo.Id, AltText("el", "Η είσοδος"), AltText("en", ""));

        // Assert
        await Assert.That(result.Data!.AltTexts).IsEquivalentTo([new CompanyPhotoAltTextViewDto("el", "Η είσοδος")]);
        await Assert.That(world.UnitOfWork.Entities<CompanyPhotoTranslation>()
                .Single(translation => translation.PhotoId == otherPhoto.Id).AltText)
            .IsEqualTo("Άλλη");
    }

    [Test]
    public async Task HandleUpdateCompanyPhoto_WhenALanguageIsLeftOut_ShouldKeepItsText()
    {
        // Arrange
        var world = new CompanyWorld();
        var photo = world.SeedPhoto(0);
        world.UnitOfWork.Seed(CompanyFactory.CreatePhotoTranslation(photo.Id, "Η πρόσοψη", TestIds.Languages.Greek));

        // Act
        var result = await HandleAsync(world, photo.Id, AltText("en", "The front"));

        // Assert
        await Assert.That(result.Data!.AltTexts).IsEquivalentTo(
            [new CompanyPhotoAltTextViewDto("el", "Η πρόσοψη"), new CompanyPhotoAltTextViewDto("en", "The front")]);
    }

    [Test]
    public async Task HandleUpdateCompanyPhoto_WhenTheLanguageIsUnknown_ShouldNameIt()
    {
        // Arrange
        var world = new CompanyWorld();
        var photo = world.SeedPhoto(0);

        // Act
        var result = await HandleAsync(world, photo.Id, AltText("fr", "La façade"));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Companies.InvalidPhotoAltText);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("AltTexts[0].languageCode");
    }

    [Test]
    public async Task HandleUpdateCompanyPhoto_WhenTheAltTextIsTooLong_ShouldNameIt()
    {
        // Arrange
        var world = new CompanyWorld();
        var photo = world.SeedPhoto(0);

        // Act
        var result = await HandleAsync(world, photo.Id, AltText("el", "ok"), AltText("en", new string('x', 301)));

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().PropertyName).IsEqualTo("AltTexts[1].altText");
        await Assert.That(world.UnitOfWork.Entities<CompanyPhotoTranslation>()).IsEmpty();
    }

    [Test]
    public async Task HandleUpdateCompanyPhoto_WhenThePhotoIsUnknown_ShouldReturnNotFound()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7(), AltText("el", "Η πρόσοψη"));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoNotFound);
        await Assert.That(world.UnitOfWork.Entities<CompanyPhotoTranslation>()).IsEmpty();
    }

    private static CompanyPhotoAltTextAddEditDto AltText(string languageCode, string? altText) =>
        new() { LanguageCode = languageCode, AltText = altText };

    private static Task<Result<CompanyPhotoResponse, HttpError>> HandleAsync(
        CompanyWorld world, Guid photoId, params CompanyPhotoAltTextAddEditDto[] altTexts) =>
        new UpdateCompanyPhotoHandler(world.UnitOfWork, world.Outbox, world.SettingsLock, world.FileStorageOptions)
            .ValidateAndHandleAsync(new UpdateCompanyPhotoRequest
            {
                CompanyId = world.CompanyId,
                PhotoId = photoId,
                AltTexts = altTexts
            });
}
