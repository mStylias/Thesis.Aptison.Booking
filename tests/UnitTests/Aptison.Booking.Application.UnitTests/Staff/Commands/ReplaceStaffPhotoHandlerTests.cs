using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Notifications;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Staff.Commands;

public class ReplaceStaffPhotoHandlerTests
{
    [Test]
    public async Task HandleReplaceStaffPhoto_WhenTheFileIsText_ShouldRefuseItAsInvalid()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, "text/plain", length: 12);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Staff.PhotoInvalid);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("File");
        await Assert.That(world.FileStorage.Objects).IsEmpty();
    }

    [Test]
    public async Task HandleReplaceStaffPhoto_WhenAnImageTypeCannotBeDecoded_ShouldRefuseItAsInvalid()
    {
        // Arrange
        var world = new StaffWorld();
        world.ImageProcessor.Decodes = false;

        // Act
        var result = await HandleAsync(world, "image/png", length: 12);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.PhotoInvalid);
    }

    [Test]
    public async Task HandleReplaceStaffPhoto_WhenTheFileIsOverTenMegabytes_ShouldRefuseItAsInvalid()
    {
        // Arrange
        var world = new StaffWorld();

        // Act
        var result = await HandleAsync(world, "image/jpeg", length: ReplaceStaffPhotoRequest.MaxBytes + 1);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Staff.PhotoInvalid);
    }

    [Test]
    public async Task HandleReplaceStaffPhoto_WhenAPhotoIsReplaced_ShouldStoreANewKeyAndStageTheOldOnesDeletion()
    {
        // Arrange
        var world = new StaffWorld();
        world.BarberMember.AvatarStorageKey = "public/staff/old.webp";

        // Act
        var result = await HandleAsync(world, "image/jpeg", length: 12);

        // Assert
        var newKey = world.BarberMember.AvatarStorageKey!;
        await Assert.That(newKey).StartsWith($"public/staff/{world.BarberMember.Id}/");
        await Assert.That(newKey).EndsWith(".webp");
        await Assert.That(result.Data!.AvatarUrl).IsEqualTo($"https://files.example.test/{newKey}");
        await Assert.That(world.FileStorage.Objects[newKey].ContentType).IsEqualTo("image/webp");
        await Assert.That(world.Outbox.OfType<StaffPhotoReplaced>())
            .IsEquivalentTo([new StaffPhotoReplaced("public/staff/old.webp")]);
    }

    private static async Task<Result<StaffPhotoResponse, HttpError>> HandleAsync(
        StaffWorld world, string contentType, long length)
    {
        await using var content = new MemoryStream("not an image"u8.ToArray());

        return await new ReplaceStaffPhotoHandler(
                world.UnitOfWork,
                world.Outbox,
                world.Caller,
                world.PermissionResolver,
                world.ImageProcessor,
                world.FileStorage,
                world.FileStorageOptions,
                NullLogger<ReplaceStaffPhotoHandler>.Instance)
            .HandleAsync(
                new ReplaceStaffPhotoRequest
                {
                    CompanyId = world.CompanyId,
                    CallerUserId = world.CallerUserId,
                    StaffMemberId = world.BarberMember.Id,
                    Content = content,
                    ContentType = contentType,
                    Length = length
                },
                CancellationToken.None);
    }
}
