using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class CreateCompanyPhotoHandlerTests
{
    [Test]
    public async Task HandleCreateCompanyPhoto_WhenTheShopHasPhotos_ShouldPlaceTheNewOneLast()
    {
        // Arrange
        var world = new CompanyWorld();
        world.SeedPhoto(displayOrder: 0);
        world.SeedPhoto(displayOrder: 4);
        world.ImageProcessor.SourceWidth = 4000;
        world.ImageProcessor.SourceHeight = 3000;

        // Act
        var result = await HandleAsync(world);

        // Assert
        var photo = world.UnitOfWork.Entities<CompanyPhoto>().Single(row => row.Id == result.Data!.Id);
        await Assert.That(photo.DisplayOrder).IsEqualTo((short)5);
        await Assert.That(photo.UploadedById).IsEqualTo(world.Owner.Id);
        await Assert.That(photo.StorageKey).StartsWith($"public/companies/{world.CompanyId}/photos/");
        await Assert.That(result.Data!.Width).IsEqualTo(2048);
        await Assert.That(result.Data.Height).IsEqualTo(1536);
        await Assert.That(result.Data.Url).IsEqualTo($"{CompanyWorld.PublicBaseUrl}/{photo.StorageKey}");
        await Assert.That(result.Data.AltTexts).IsEmpty();
        await Assert.That(world.FileStorage.Objects.Keys).IsEquivalentTo([photo.StorageKey]);
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleCreateCompanyPhoto_WhenTheShopHasNoPhotos_ShouldMakeItTheMainOne()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world);

        // Assert
        await Assert.That(world.UnitOfWork.Entities<CompanyPhoto>().Single(row => row.Id == result.Data!.Id).DisplayOrder)
            .IsEqualTo((short)0);
    }

    [Test]
    public async Task HandleCreateCompanyPhoto_WhenTheShopHasTwentyPhotos_ShouldRefuseWithTheLimit()
    {
        // Arrange
        var world = new CompanyWorld();

        for (short order = 0; order < 20; order++)
        {
            world.SeedPhoto(order);
        }

        // Act
        var result = await HandleAsync(world);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoLimitReached);
        await Assert.That(((HttpErrorWithExtensions)result.Error).Extensions["limit"]).IsEqualTo(20);
        await Assert.That(world.UnitOfWork.Entities<CompanyPhoto>().Count).IsEqualTo(20);
        await Assert.That(world.FileStorage.Objects).IsEmpty();
    }

    [Test]
    public async Task HandleCreateCompanyPhoto_WhenTheLimitIsReachedUnderTheLock_ShouldDeleteTheUploadedObject()
    {
        // Arrange
        var world = new CompanyWorld();

        for (short order = 0; order < 20; order++)
        {
            world.SeedPhoto(order);
        }

        // Act
        var result = await HandleAsync(world, skipFirstCount: true);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoLimitReached);
        await Assert.That(world.FileStorage.Objects).IsEmpty();
        await Assert.That(world.FileStorage.DeletedKeys.Count).IsEqualTo(1);
    }

    [Test]
    public async Task HandleCreateCompanyPhoto_WhenTheShorterSideIsUnder600Pixels_ShouldRefuseAsTooSmall()
    {
        // Arrange
        var world = new CompanyWorld();
        world.ImageProcessor.SourceWidth = 800;
        world.ImageProcessor.SourceHeight = 500;

        // Act
        var result = await HandleAsync(world);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoTooSmall);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("File");
        await Assert.That(world.FileStorage.Objects).IsEmpty();
    }

    [Test]
    [Arguments("image/gif", 1024L)]
    [Arguments("image/jpeg", 15L * 1024 * 1024 + 1)]
    public async Task HandleCreateCompanyPhoto_WhenTheUploadIsNotAccepted_ShouldRefuseOnTheFile(string contentType, long length)
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, contentType, length);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoInvalid);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("File");
    }

    [Test]
    public async Task HandleCreateCompanyPhoto_WhenTheImageCannotBeDecoded_ShouldRefuseOnTheFile()
    {
        // Arrange
        var world = new CompanyWorld();
        world.ImageProcessor.Decodes = false;

        // Act
        var result = await HandleAsync(world);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoInvalid);
        await Assert.That(world.UnitOfWork.Entities<CompanyPhoto>()).IsEmpty();
    }

    /// <param name="skipFirstCount">Hides the seeded photos from the early count, as another owner's upload would.</param>
    private static async Task<Result<CompanyPhotoResponse, HttpError>> HandleAsync(
        CompanyWorld world, string contentType = "image/jpeg", long length = 1024, bool skipFirstCount = false)
    {
        var seeded = world.UnitOfWork.Entities<CompanyPhoto>().ToList();

        if (skipFirstCount)
        {
            foreach (var photo in seeded)
            {
                photo.IsDeleted = true;
            }

            world.ImageProcessor.OnScale = () =>
            {
                foreach (var photo in seeded)
                {
                    photo.IsDeleted = false;
                }
            };
        }

        return await new CreateCompanyPhotoHandler(
                world.UnitOfWork, world.Outbox, world.SettingsLock, world.ImageProcessor, world.FileStorage,
                world.FileStorageOptions, NullLogger<CreateCompanyPhotoHandler>.Instance)
            .ValidateAndHandleAsync(new CreateCompanyPhotoRequest
            {
                CompanyId = world.CompanyId,
                CallerUserId = world.Owner.Id,
                Content = new MemoryStream([1, 2, 3]),
                ContentType = contentType,
                Length = length
            });
    }
}
