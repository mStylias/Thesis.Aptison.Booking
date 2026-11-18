using Aptison.Booking.Application.Abstractions.Images;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Common.Rules;
using Aptison.Booking.Application.Features.Companies.Notifications;
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

/// <summary>
/// Stores one 512 × 512 WebP under a new key, so the URL changes with the logo and the old one can
/// be cached for ever.
/// </summary>
public class ReplaceCompanyLogoRequest : ICommand<Result<CompanyLogoResponse, HttpError>>
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int SizePixels = 512;

    public Guid CompanyId { get; set; }

    /// <summary>Owned by the caller, who disposes it.</summary>
    public Stream Content { get; set; } = Stream.Null;

    public string? ContentType { get; set; }
    public long Length { get; set; }
}

public class ReplaceCompanyLogoHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICompanySettingsLock settingsLock,
    IImageProcessor imageProcessor,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> fileStorageOptions,
    ILogger<ReplaceCompanyLogoHandler> logger)
    : ICommandHandler<ReplaceCompanyLogoRequest, Result<CompanyLogoResponse, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();

    public async Task<Result<CompanyLogoResponse, HttpError>> HandleAsync(
        ReplaceCompanyLogoRequest request, CancellationToken cancellationToken)
    {
        if (!CompanyImageRules.IsAcceptedUpload(request.ContentType, request.Length, ReplaceCompanyLogoRequest.MaxBytes))
        {
            return CreateInvalidError();
        }

        var image = await imageProcessor.CreateSquareWebpAsync(
            request.Content, ReplaceCompanyLogoRequest.SizePixels, cancellationToken);

        if (image is null)
        {
            return CreateInvalidError();
        }

        var storageKey = $"public/companies/{request.CompanyId}/logo/{Guid.CreateVersion7()}.webp";

        try
        {
            using var content = new MemoryStream(image.Content, writable: false);
            await fileStorage.PutAsync(storageKey, content, image.ContentType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Storing the logo of company {CompanyId} failed", request.CompanyId);
            return CompanySettingsErrors.CreateUpdateFailed();
        }

        // A failure here leaves an orphan object and no row, which is acceptable.
        try
        {
            return await CompanySettingsTransaction.RunAsync(
                unitOfWork, settingsLock, request.CompanyId,
                () => SaveAsync(request.CompanyId, storageKey, cancellationToken),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CompanySettingsErrors.CreateUpdateFailed();
        }
    }

    private async Task<Result<CompanyLogoResponse, HttpError>> SaveAsync(
        Guid companyId, string storageKey, CancellationToken cancellationToken)
    {
        var company = await _companyRepo
            .Query(query => query.Where(row => row.Id == companyId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return CompanySettingsErrors.CreateNotFound();
        }

        if (company.LogoStorageKey is { } oldStorageKey)
        {
            await outbox.EnqueueAsync(new CompanyImageRemoved(oldStorageKey), cancellationToken);
        }

        company.LogoStorageKey = storageKey;

        // The catalog flag evicts the cached shop and booking pages, which show the logo.
        await outbox.EnqueueAsync(new CompanyCalendarChanged(companyId, CatalogChanged: true), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CompanyLogoResponse(
            FileStorageUtils.BuildPublicUrl(storageKey, fileStorageOptions.Value.PublicBaseUrl));
    }

    private static HttpError CreateInvalidError() =>
        HttpError.BadRequest(
                "The logo must be a JPEG, PNG or WebP image of at most 10 MB.",
                errorCode: ErrorCodes.Companies.LogoInvalid)
            .WithProperty(CompanyImageRules.PropertyName);
}
