using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Images;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Application.Features.Staff.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

/// <summary>
/// Stores one 512 × 512 WebP under a new key, so the URL changes with the photo and the old one can
/// be cached for ever.
/// </summary>
public class ReplaceStaffPhotoRequest : ICommand<Result<StaffPhotoResponse, HttpError>>
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int SizePixels = 512;

    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }

    /// <summary>Owned by the caller, who disposes it.</summary>
    public Stream Content { get; set; } = Stream.Null;

    public string? ContentType { get; set; }
    public long Length { get; set; }
}

public class ReplaceStaffPhotoHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IImageProcessor imageProcessor,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> fileStorageOptions,
    ILogger<ReplaceStaffPhotoHandler> logger)
    : ICommandHandler<ReplaceStaffPhotoRequest, Result<StaffPhotoResponse, HttpError>>
{
    private const string PropertyName = "File";

    private static readonly HashSet<string> AcceptedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();

    public async Task<Result<StaffPhotoResponse, HttpError>> HandleAsync(
        ReplaceStaffPhotoRequest request, CancellationToken cancellationToken)
    {
        var member = await _staffMemberRepo
            .Query(query => query.Where(row => row.Id == request.StaffMemberId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return StaffErrors.CreateNotFound();
        }

        var mayManage = await StaffRankRules.EnsureMayManageAsync(
            permissionResolver, caller, request.CallerUserId, member.UserId, request.CompanyId,
            refuseSelf: false, cancellationToken);

        if (mayManage.IsError)
        {
            return mayManage.Error!;
        }

        if (request.Length is <= 0 or > ReplaceStaffPhotoRequest.MaxBytes
            || request.ContentType is null
            || !AcceptedContentTypes.Contains(request.ContentType))
        {
            return CreateInvalidError();
        }

        var image = await imageProcessor.CreateSquareWebpAsync(
            request.Content, ReplaceStaffPhotoRequest.SizePixels, cancellationToken);

        if (image is null)
        {
            return CreateInvalidError();
        }

        var storageKey = $"public/staff/{member.Id}/{Guid.CreateVersion7()}.webp";

        try
        {
            using var content = new MemoryStream(image.Content, writable: false);
            await fileStorage.PutAsync(storageKey, content, image.ContentType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Storing the photo of staff member {StaffMemberId} failed", member.Id);

            return HttpError.Internal("The photo could not be stored. Please try again.",
                errorCode: ErrorCodes.Staff.UpdateFailed);
        }

        var oldStorageKey = member.AvatarStorageKey;

        // A failure here leaves an orphan object and no row, which is acceptable.
        var saved = await Result.TryAsync(
            async () =>
            {
                member.AvatarStorageKey = storageKey;

                if (oldStorageKey is not null)
                {
                    await outbox.EnqueueAsync(new StaffPhotoReplaced(oldStorageKey), cancellationToken);
                }

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The photo could not be saved. Please try again.",
                errorCode: ErrorCodes.Staff.UpdateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return new StaffPhotoResponse(
            FileStorageUtils.BuildPublicUrl(storageKey, fileStorageOptions.Value.PublicBaseUrl));
    }

    private static HttpError CreateInvalidError() =>
        HttpError.BadRequest(
                "The photo must be a JPEG, PNG or WebP image of at most 10 MB.",
                errorCode: ErrorCodes.Staff.PhotoInvalid)
            .WithProperty(PropertyName);
}
