using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Translations;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Companies.Common.Readers;

/// <summary>The shop's photos in display order. Relies on the ownership filter; the translations carry no company.</summary>
internal static class CompanyPhotoReader
{
    public static async Task<List<CompanyPhotoResponse>> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        string? publicBaseUrl,
        CancellationToken cancellationToken,
        Guid? photoId = null)
    {
        var photos = await unitOfWork.GetRepository<CompanyPhoto>()
            .QuerySelect(
                photo => new { photo.Id, photo.StorageKey, photo.Width, photo.Height },
                query => query
                    .Where(photo => photoId == null || photo.Id == photoId)
                    .OrderBy(photo => photo.DisplayOrder)
                    .ThenBy(photo => photo.Id))
            .ToListAsync(cancellationToken);

        var photoIds = photos.Select(photo => photo.Id).ToList();

        var altTexts = await unitOfWork.GetRepository<CompanyPhotoTranslation>()
            .QuerySelect(
                translation => new { translation.PhotoId, translation.LanguageId, translation.AltText },
                query => query.Where(translation => photoIds.Contains(translation.PhotoId)))
            .ToListAsync(cancellationToken);

        var languageIds = altTexts.Select(translation => translation.LanguageId).Distinct().ToList();
        var languageCodesByIds = await unitOfWork.GetRepository<Language>()
            .QuerySelect(
                language => new { language.Id, language.Code },
                query => query.Where(language => languageIds.Contains(language.Id)))
            .ToDictionaryAsync(language => language.Id, language => language.Code, cancellationToken);

        var altTextsByPhotoIds = altTexts
            .Where(translation => languageCodesByIds.ContainsKey(translation.LanguageId))
            .ToLookup(
                translation => translation.PhotoId,
                translation => new CompanyPhotoAltTextViewDto(
                    languageCodesByIds[translation.LanguageId], translation.AltText));

        return
        [
            .. photos.Select(photo => new CompanyPhotoResponse(
                photo.Id,
                // A relative key is only unreachable while Storage:PublicBaseUrl is unset.
                FileStorageUtils.BuildPublicUrl(photo.StorageKey, publicBaseUrl) ?? photo.StorageKey,
                photo.Width,
                photo.Height,
                [.. altTextsByPhotoIds[photo.Id].OrderBy(altText => altText.LanguageCode, StringComparer.Ordinal)]))
        ];
    }
}
