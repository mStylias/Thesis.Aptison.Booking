using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Translations;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>Sets or clears the alt text of the languages named. A language left out keeps its text.</summary>
public class UpdateCompanyPhotoRequest : ICommand<Result<CompanyPhotoResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid PhotoId { get; set; }

    public IReadOnlyList<CompanyPhotoAltTextAddEditDto> AltTexts { get; set; } = [];
}

public class UpdateCompanyPhotoHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICompanySettingsLock settingsLock,
    IOptions<FileStorageOptions> fileStorageOptions)
    : ICommandHandler<UpdateCompanyPhotoRequest, Result<CompanyPhotoResponse, HttpError>>,
        IWithValidation<UpdateCompanyPhotoRequest, Result<Dictionary<string, string?>, HttpError>>
{
    private readonly IRepository<CompanyPhoto, BookingContext> _photoRepo = unitOfWork.GetRepository<CompanyPhoto>();
    private readonly IRepository<CompanyPhotoTranslation, BookingContext> _photoTranslationRepo =
        unitOfWork.GetRepository<CompanyPhotoTranslation>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public Task<Result<Dictionary<string, string?>, HttpError>> ValidateAsync(
        UpdateCompanyPhotoRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(CompanyPhotoRules.ParseAltTexts(request.AltTexts ?? [], nameof(request.AltTexts)));

    public async Task<Result<CompanyPhotoResponse, HttpError>> HandleAsync(
        UpdateCompanyPhotoRequest request, CancellationToken cancellationToken)
    {
        var altTextsByLanguageCodes = this.ValidationData();

        Result<bool, HttpError> saved;

        try
        {
            saved = await CompanySettingsTransaction.RunAsync(
                unitOfWork, settingsLock, request.CompanyId,
                () => SaveAsync(request, altTextsByLanguageCodes, cancellationToken),
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

        var stored = await CompanyPhotoReader.ReadAsync(
            unitOfWork, fileStorageOptions.Value.PublicBaseUrl, cancellationToken, request.PhotoId);

        return stored.Count == 0 ? CompanyPhotoRules.CreateNotFoundError() : stored[0];
    }

    private async Task<Result<bool, HttpError>> SaveAsync(
        UpdateCompanyPhotoRequest request,
        Dictionary<string, string?> altTextsByLanguageCodes,
        CancellationToken cancellationToken)
    {
        // The translations carry no company, so the photo's own read is what scopes them.
        var photoExists = await _photoRepo
            .Query(query => query.Where(photo => photo.Id == request.PhotoId))
            .AnyAsync(cancellationToken);

        if (!photoExists)
        {
            return CompanyPhotoRules.CreateNotFoundError();
        }

        var languageCodes = altTextsByLanguageCodes.Keys.ToList();
        var languageIdsByCodes = await _languageRepo
            .QuerySelect(
                language => new { language.Id, language.Code },
                query => query.Where(language => languageCodes.Contains(language.Code)))
            .ToDictionaryAsync(language => language.Code, language => language.Id, cancellationToken);

        var languageIds = languageIdsByCodes.Values.ToList();
        var existingByLanguageIds = await _photoTranslationRepo
            .Query(
                query => query.Where(translation =>
                    translation.PhotoId == request.PhotoId && languageIds.Contains(translation.LanguageId)),
                asNoTracking: false)
            .ToDictionaryAsync(translation => translation.LanguageId, cancellationToken);

        foreach (var (languageCode, altText) in altTextsByLanguageCodes)
        {
            var languageId = languageIdsByCodes[languageCode];
            var existing = existingByLanguageIds.GetValueOrDefault(languageId);

            if (altText is null)
            {
                if (existing is not null)
                {
                    _photoTranslationRepo.Remove(existing);
                }
            }
            else if (existing is not null)
            {
                existing.AltText = altText;
            }
            else
            {
                await _photoTranslationRepo.AddAsync(
                    new CompanyPhotoTranslation { PhotoId = request.PhotoId, LanguageId = languageId, AltText = altText },
                    cancellationToken);
            }
        }

        // The catalog flag evicts the cached shop page, which shows the alt text.
        await outbox.EnqueueAsync(new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
