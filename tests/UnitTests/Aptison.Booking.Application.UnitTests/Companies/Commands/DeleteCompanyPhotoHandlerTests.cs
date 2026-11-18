using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Notifications;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class DeleteCompanyPhotoHandlerTests
{
    [Test]
    public async Task HandleDeleteCompanyPhoto_WhenThePhotoExists_ShouldSoftDeleteItAndStageItsFile()
    {
        // Arrange
        var world = new CompanyWorld();
        var photo = world.SeedPhoto(0);

        // Act
        var result = await HandleAsync(world, photo.Id);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(photo.IsDeleted).IsTrue();
        await Assert.That(world.Outbox.OfType<CompanyImageRemoved>())
            .IsEquivalentTo([new CompanyImageRemoved(photo.StorageKey)]);
    }

    [Test]
    public async Task HandleDeleteCompanyPhoto_WhenThePhotoIsUnknown_ShouldReturnNotFound()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7());

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.PhotoNotFound);
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    private static Task<Result<HttpError>> HandleAsync(CompanyWorld world, Guid photoId) =>
        new DeleteCompanyPhotoHandler(world.UnitOfWork, world.Outbox, world.SettingsLock)
            .HandleAsync(new DeleteCompanyPhotoRequest { CompanyId = world.CompanyId, PhotoId = photoId }, default);
}
