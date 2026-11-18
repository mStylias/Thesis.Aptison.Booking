using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Notifications;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class ReplaceCompanyLogoHandlerTests
{
    [Test]
    public async Task HandleReplaceCompanyLogo_WhenTheImageIsValid_ShouldStoreItUnderANewKeyAndReturnItsUrl()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world);

        // Assert
        var storageKey = world.FileStorage.Objects.Keys.Single();
        await Assert.That(storageKey).StartsWith($"public/companies/{world.CompanyId}/logo/");
        await Assert.That(world.Company.LogoStorageKey).IsEqualTo(storageKey);
        await Assert.That(result.Data).IsEqualTo(new CompanyLogoResponse($"{CompanyWorld.PublicBaseUrl}/{storageKey}"));
        await Assert.That(world.SettingsLock.Acquired).IsEquivalentTo([world.CompanyId]);
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleReplaceCompanyLogo_WhenALogoIsReplaced_ShouldStageTheOldOneForDeletion()
    {
        // Arrange
        var world = new CompanyWorld();
        world.Company.LogoStorageKey = "public/companies/old-logo.webp";

        // Act
        await HandleAsync(world);

        // Assert
        await Assert.That(world.Outbox.OfType<CompanyImageRemoved>())
            .IsEquivalentTo([new CompanyImageRemoved("public/companies/old-logo.webp")]);
        await Assert.That(world.Company.LogoStorageKey).IsNotEqualTo("public/companies/old-logo.webp");
    }

    [Test]
    public async Task HandleReplaceCompanyLogo_WhenTheShopHadNoLogo_ShouldStageNoDeletion()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        await HandleAsync(world);

        // Assert
        await Assert.That(world.Outbox.OfType<CompanyImageRemoved>()).IsEmpty();
    }

    [Test]
    [Arguments("image/gif", 1024L)]
    [Arguments(null, 1024L)]
    [Arguments("image/png", 10L * 1024 * 1024 + 1)]
    [Arguments("image/png", 0L)]
    public async Task HandleReplaceCompanyLogo_WhenTheUploadIsNotAccepted_ShouldRefuseOnTheFile(string? contentType, long length)
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, contentType, length);

        // Assert
        await AssertInvalidAsync(world, result);
    }

    [Test]
    public async Task HandleReplaceCompanyLogo_WhenTheImageCannotBeDecoded_ShouldRefuseOnTheFile()
    {
        // Arrange
        var world = new CompanyWorld();
        world.ImageProcessor.Decodes = false;

        // Act
        var result = await HandleAsync(world);

        // Assert
        await AssertInvalidAsync(world, result);
    }

    private static async Task AssertInvalidAsync(CompanyWorld world, Result<CompanyLogoResponse, HttpError> result)
    {
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.LogoInvalid);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("File");
        await Assert.That(world.FileStorage.Objects).IsEmpty();
        await Assert.That(world.Company.LogoStorageKey).IsNull();
    }

    private static Task<Result<CompanyLogoResponse, HttpError>> HandleAsync(
        CompanyWorld world, string? contentType = "image/png", long length = 1024) =>
        new ReplaceCompanyLogoHandler(
                world.UnitOfWork, world.Outbox, world.SettingsLock, world.ImageProcessor, world.FileStorage,
                world.FileStorageOptions, NullLogger<ReplaceCompanyLogoHandler>.Instance)
            .ValidateAndHandleAsync(new ReplaceCompanyLogoRequest
            {
                CompanyId = world.CompanyId,
                Content = new MemoryStream([1, 2, 3]),
                ContentType = contentType,
                Length = length
            });
}
