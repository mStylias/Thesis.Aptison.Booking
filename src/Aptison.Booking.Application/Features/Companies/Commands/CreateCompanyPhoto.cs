using Aptison.Booking.Application.Abstractions.Images;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>Stored as WebP with its aspect ratio kept, and placed last.</summary>
public class CreateCompanyPhotoRequest : ICommand<Result<CompanyPhotoResponse, HttpError>>
{
    public const long MaxBytes = 15 * 1024 * 1024;

    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }

    /// <summary>Owned by the caller, who disposes it.</summary>
    public Stream Content { get; set; } = Stream.Null;

    public string? ContentType { get; set; }
    public long Length { get; set; }
}

public class CreateCompanyPhotoHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICompanySettingsLock settingsLock,
    IImageProcessor imageProcessor,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> fileStorageOptions,
    ILogger<CreateCompanyPhotoHandler> logger)
    : ICommandHandler<CreateCompanyPhotoRequest, Result<CompanyPhotoResponse, HttpError>>
{
    private readonly IRepository<CompanyPhoto, BookingContext> _photoRepo = unitOfWork.GetRepository<CompanyPhoto>();

    public async Task<Result<CompanyPhotoResponse, HttpError>> HandleAsync(
        CreateCompanyPhotoRequest request, CancellationToken cancellationToken)
    {
        if (!CompanyImageRules.IsAcceptedUpload(request.ContentType, request.Length, CreateCompanyPhotoRequest.MaxBytes))
        {
            return CreateInvalidError();
        }

        // Checked again under the lock; this only spares the processing and the upload.
        if (await _photoRepo.Query(query => query).CountAsync(cancellationToken) >= CompanyPhotoRules.MaxPhotos)
        {
            return CreateLimitReachedError();
        }

        var scaled = await imageProcessor.CreateScaledWebpAsync(
            request.Content, CompanyPhotoRules.MaxSidePixels, cancellationToken);

        if (scaled is null)
        {
            return CreateInvalidError();
        }

        if (Math.Min(scaled.SourceWidth, scaled.SourceHeight) < CompanyPhotoRules.MinShorterSidePixels)
        {
            return HttpError.BadRequest(
                    $"The photo must be at least {CompanyPhotoRules.MinShorterSidePixels} pixels on its shorter side.",
                    errorCode: ErrorCodes.Companies.PhotoTooSmall)
                .WithProperty(CompanyImageRules.PropertyName);
        }

        var image = scaled.Image;
        var storageKey = $"public/companies/{request.CompanyId}/photos/{Guid.CreateVersion7()}.webp";

        try
        {
            using var content = new MemoryStream(image.Content, writable: false);
            await fileStorage.PutAsync(storageKey, content, image.ContentType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Storing a photo of company {CompanyId} failed", request.CompanyId);
            return CompanySettingsErrors.CreateUpdateFailed();
        }

        var photo = new CompanyPhoto
        {
            Id = Guid.CreateVersion7(),
            OwnerId = request.CompanyId,
            StorageKey = storageKey,
            ContentType = image.ContentType,
            Width = image.Width,
            Height = image.Height,
            SizeBytes = image.Content.Length,
            UploadedById = request.CallerUserId
        };

        Result<bool, HttpError> saved;

        try
        {
            saved = await CompanySettingsTransaction.RunAsync(
                unitOfWork, settingsLock, request.CompanyId, () => AddLastAsync(photo, cancellationToken),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            saved = CompanySettingsErrors.CreateUpdateFailed();
        }

        if (saved.IsError)
        {
            await DeleteUnsavedAsync(storageKey, request.CompanyId);
            return saved.Error!;
        }

        var stored = await CompanyPhotoReader.ReadAsync(
            unitOfWork, fileStorageOptions.Value.PublicBaseUrl, cancellationToken, photo.Id);

        return stored.Count == 0 ? CompanyPhotoRules.CreateNotFoundError() : stored[0];
    }

    private async Task<Result<bool, HttpError>> AddLastAsync(CompanyPhoto photo, CancellationToken cancellationToken)
    {
        var displayOrders = await _photoRepo
            .QuerySelect(row => row.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (displayOrders.Count >= CompanyPhotoRules.MaxPhotos)
        {
            return CreateLimitReachedError();
        }

        photo.DisplayOrder = (short)(displayOrders.Count == 0 ? 0 : displayOrders.Max() + 1);

        await _photoRepo.AddAsync(photo, cancellationToken);
        // The catalog flag evicts the cached shop page, which shows the photos.
        await outbox.EnqueueAsync(new CompanyCalendarChanged(photo.OwnerId, CatalogChanged: true), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // Best effort: no row points at the object, so a failure only leaves an orphan.
    private async Task DeleteUnsavedAsync(string storageKey, Guid companyId)
    {
        try
        {
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Deleting an unsaved photo of company {CompanyId} failed", companyId);
        }
    }

    private static HttpError CreateInvalidError() =>
        HttpError.BadRequest(
                "The photo must be a JPEG, PNG or WebP image of at most 15 MB.",
                errorCode: ErrorCodes.Companies.PhotoInvalid)
            .WithProperty(CompanyImageRules.PropertyName);

    private static HttpErrorWithExtensions CreateLimitReachedError() =>
        HttpErrorWithExtensions.Create(
            HttpError.Conflict(
                $"A shop has at most {CompanyPhotoRules.MaxPhotos} photos.",
                errorCode: ErrorCodes.Companies.PhotoLimitReached),
            new Dictionary<string, object?> { ["limit"] = CompanyPhotoRules.MaxPhotos });
}
