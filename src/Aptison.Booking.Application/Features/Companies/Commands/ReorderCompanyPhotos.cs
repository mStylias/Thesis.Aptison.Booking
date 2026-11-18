using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>
/// Names every current photo exactly once, first the main one. Anything else, such as a list read
/// before another owner added a photo, is refused rather than guessed at.
/// </summary>
public class ReorderCompanyPhotosRequest : ICommand<Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public IReadOnlyList<Guid> PhotoIds { get; set; } = [];
}

public class ReorderCompanyPhotosHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICompanySettingsLock settingsLock,
    IOptions<FileStorageOptions> fileStorageOptions)
    : ICommandHandler<ReorderCompanyPhotosRequest, Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>>
{
    private readonly IRepository<CompanyPhoto, BookingContext> _photoRepo = unitOfWork.GetRepository<CompanyPhoto>();

    public async Task<Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>> HandleAsync(
        ReorderCompanyPhotosRequest request, CancellationToken cancellationToken)
    {
        Result<bool, HttpError> saved;

        try
        {
            saved = await CompanySettingsTransaction.RunAsync(
                unitOfWork, settingsLock, request.CompanyId,
                () => SaveAsync(request.CompanyId, request.PhotoIds ?? [], cancellationToken),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            saved = CompanySettingsErrors.CreateUpdateFailed();
        }

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return await CompanyPhotoReader.ReadAsync(unitOfWork, fileStorageOptions.Value.PublicBaseUrl, cancellationToken);
    }

    private async Task<Result<bool, HttpError>> SaveAsync(
        Guid companyId, IReadOnlyList<Guid> photoIds, CancellationToken cancellationToken)
    {
        var photos = await _photoRepo
            .Query(query => query, asNoTracking: false)
            .ToListAsync(cancellationToken);

        var isEveryPhotoOnce = photoIds.Count == photos.Count
                               && photoIds.Distinct().Count() == photoIds.Count
                               && photos.All(photo => photoIds.Contains(photo.Id));

        if (!isEveryPhotoOnce)
        {
            return HttpError.Conflict(
                "The photos changed since they were loaded. Please reload and try again.",
                errorCode: ErrorCodes.Companies.PhotoOrderMismatch);
        }

        var positionsByPhotoIds = photoIds.Select((photoId, position) => (photoId, position))
            .ToDictionary(pair => pair.photoId, pair => (short)pair.position);

        foreach (var photo in photos)
        {
            photo.DisplayOrder = positionsByPhotoIds[photo.Id];
        }

        // The catalog flag evicts the cached shop page, whose first photo is the main one.
        await outbox.EnqueueAsync(new CompanyCalendarChanged(companyId, CatalogChanged: true), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
