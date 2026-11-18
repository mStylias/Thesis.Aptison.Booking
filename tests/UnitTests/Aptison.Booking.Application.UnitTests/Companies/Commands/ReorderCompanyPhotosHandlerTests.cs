using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;
using TUnit.Assertions.Enums;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class ReorderCompanyPhotosHandlerTests
{
    [Test]
    public async Task HandleReorderCompanyPhotos_WhenEveryPhotoIsNamedOnce_ShouldStoreAndReturnTheNewOrder()
    {
        // Arrange
        var world = new CompanyWorld();
        var first = world.SeedPhoto(0);
        var second = world.SeedPhoto(1);
        var third = world.SeedPhoto(2);

        // Act
        var result = await HandleAsync(world, third.Id, first.Id, second.Id);

        // Assert
        await Assert.That(result.Data!.Select(photo => photo.Id))
            .IsEquivalentTo([third.Id, first.Id, second.Id], CollectionOrdering.Matching);
        await Assert.That(third.DisplayOrder).IsEqualTo((short)0);
        await Assert.That(first.DisplayOrder).IsEqualTo((short)1);
        await Assert.That(second.DisplayOrder).IsEqualTo((short)2);
    }

    [Test]
    public async Task HandleReorderCompanyPhotos_WhenAPhotoIsLeftOut_ShouldRefuseAsMismatch()
    {
        // Arrange
        var world = new CompanyWorld();
        var first = world.SeedPhoto(0);
        world.SeedPhoto(1);

        // Act
        var result = await HandleAsync(world, first.Id);

        // Assert
        await AssertMismatchAsync(result);
    }

    [Test]
    public async Task HandleReorderCompanyPhotos_WhenAPhotoIsNamedTwice_ShouldRefuseAsMismatch()
    {
        // Arrange
        var world = new CompanyWorld();
        var first = world.SeedPhoto(0);
        var second = world.SeedPhoto(1);

        // Act
        var result = await HandleAsync(world, second.Id, first.Id, first.Id);

        // Assert
        await AssertMismatchAsync(result);
        await Assert.That(first.DisplayOrder).IsEqualTo((short)0);
    }

    [Test]
    public async Task HandleReorderCompanyPhotos_WhenAnUnknownPhotoIsNamed_ShouldRefuseAsMismatch()
    {
        // Arrange
        var world = new CompanyWorld();
        var first = world.SeedPhoto(0);

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7());

        // Assert
        await AssertMismatchAsync(result);
        await Assert.That(first.DisplayOrder).IsEqualTo((short)0);
    }

    private static async Task AssertMismatchAsync(Result<IReadOnlyList<CompanyPhotoResponse>, HttpError> result)
    {
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoOrderMismatch);
    }

    private static Task<Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>> HandleAsync(
        CompanyWorld world, params Guid[] photoIds) =>
        new ReorderCompanyPhotosHandler(world.UnitOfWork, world.Outbox, world.SettingsLock, world.FileStorageOptions)
            .ValidateAndHandleAsync(new ReorderCompanyPhotosRequest { CompanyId = world.CompanyId, PhotoIds = photoIds });
}
