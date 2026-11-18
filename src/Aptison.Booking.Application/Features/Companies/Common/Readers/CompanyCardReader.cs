using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Translations;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;

namespace Aptison.Booking.Application.Features.Companies.Common.Readers;

/// <summary>
/// The company cards, ordered by distance from the origin. Shared by the browse endpoint and the
/// cached <c>/all</c> one, which is the same read with no filters.
/// <para>
/// Separate queries, not a navigation projection: the display name needs a per-request language with a
/// fallback, which no mapper profile can express — a profile is one expression built at startup.
/// </para>
/// </summary>
internal static class CompanyCardReader
{
    public static async Task<IReadOnlyList<CompanyCardResponse>> ReadAsync(
        IRepository<Company, BookingContext> companies,
        IRepository<CompanyTranslation, BookingContext> companyTranslations,
        IRepository<CompanyPhoto, BookingContext> companyPhotos,
        IRepository<Language, BookingContext> languages,
        IUserContextScope userContext,
        CompanyCardCriteria criteria,
        string? publicBaseUrl,
        CancellationToken cancellationToken)
    {
        var companyRows = await companies
            .QuerySelect(
                company => new
                {
                    company.Id,
                    company.Slug,
                    company.LogoStorageKey,
                    company.PostalCode,
                    company.CountryCode,
                    company.Timezone,
                    company.Currency,
                    company.SlotIntervalMinutes,
                    company.Location,
                    DistanceMeters = company.Location.Distance(criteria.Origin),
                    PrimaryBusinessTypeCode = company.BusinessTypes
                        .Where(businessType => businessType.IsPrimary)
                        .Select(businessType => businessType.BusinessType.Code)
                        .FirstOrDefault()
                },
                query => ApplyFilters(query, criteria)
                    // Ordering the entity query rather than the projection keeps ST_Distance in
                    // SQL, where ix_companies_location can serve it.
                    .OrderBy(company => company.Location.Distance(criteria.Origin))
                    // Two shops at one address would otherwise swap between requests.
                    .ThenBy(company => company.Id))
            .ToListAsync(cancellationToken);

        if (companyRows.Count == 0)
        {
            return [];
        }

        var companyIds = companyRows.Select(company => company.Id).ToList();
        var preferredLanguageId = await ReadLanguageIdAsync(languages, criteria.LanguageCode, cancellationToken);

        var translations = await companyTranslations
            .QuerySelect(
                translation => new
                {
                    translation.CompanyId,
                    translation.LanguageId,
                    translation.Name,
                    translation.Description,
                    translation.Street,
                    translation.City,
                    translation.Region
                },
                query => query.Where(translation => companyIds.Contains(translation.CompanyId)))
            .ToListAsync(cancellationToken);

        var translationsByCompanyId = translations
            .GroupBy(translation => translation.CompanyId)
            .ToDictionary(company => company.Key, company => company.ToList());

        var mainPhotoKeysByCompanyIds = await ReadMainPhotoKeysAsync(
            companyPhotos, userContext, companyIds, cancellationToken);

        var cards = new List<CompanyCardResponse>(companyRows.Count);

        foreach (var company in companyRows)
        {
            if (!translationsByCompanyId.TryGetValue(company.Id, out var companyTranslationRows))
            {
                // A company with no translation at all has no name to show, so it is dropped rather
                // than rendered blank.
                continue;
            }

            // Ordered by language id so two requests asking for an unavailable language agree on
            // which fallback they get.
            var translation =
                companyTranslationRows.FirstOrDefault(row => row.LanguageId == preferredLanguageId)
                ?? companyTranslationRows.OrderBy(row => row.LanguageId).First();

            cards.Add(new CompanyCardResponse(
                company.Id,
                company.Slug,
                translation.Name,
                translation.Description,
                translation.Street,
                translation.City,
                translation.Region,
                company.PostalCode,
                company.CountryCode,
                FileStorageUtils.BuildPublicUrl(company.LogoStorageKey, publicBaseUrl),
                FileStorageUtils.BuildPublicUrl(mainPhotoKeysByCompanyIds.GetValueOrDefault(company.Id), publicBaseUrl),
                company.Location.Y,
                company.Location.X,
                company.DistanceMeters,
                company.Timezone,
                company.Currency,
                company.SlotIntervalMinutes,
                company.PrimaryBusinessTypeCode));
        }

        return cards;
    }

    private static IQueryable<Company> ApplyFilters(IQueryable<Company> query, CompanyCardCriteria criteria)
    {
        query = query
            .Where(company => company.IsActive)
            .Where(company => company.BusinessTypes.Any(businessType =>
                businessType.IsPrimary && businessType.BusinessType.Code == criteria.BusinessTypeCode));

        if (criteria.SearchPattern is { } pattern)
        {
            // name_normalized, never name: the column is folded and the term was folded to match.
            query = query.Where(company => company.Translations.Any(translation =>
                EF.Functions.Like(translation.NameNormalized, pattern, CompanySearchTerm.LikeEscape)));
        }

        return ApplyAvailability(query, criteria);
    }

    /// <summary>
    /// The availability filter. Slots carry the time and offerings carry the catalog, so with no
    /// service or category asked for the offerings join drops out and the common case stays a
    /// single <c>EXISTS</c> over the index.
    /// </summary>
    private static IQueryable<Company> ApplyAvailability(IQueryable<Company> query, CompanyCardCriteria criteria)
    {
        var serviceIds = criteria.ServiceIds ?? [];
        var categoryIds = criteria.CategoryIds ?? [];
        var wantsCatalog = serviceIds.Length > 0 || categoryIds.Length > 0;

        if (criteria.Date is null && criteria.FromMinute is null && criteria.ToMinute is null && !wantsCatalog)
        {
            return query;
        }

        // Each condition is dropped rather than widened when its parameter is absent: with a time
        // range but no date, "available in that range on some day in the horizon" is the question.
        var date = criteria.Date;
        var fromMinute = criteria.FromMinute;
        var toMinute = criteria.ToMinute;

        if (!wantsCatalog)
        {
            return query.Where(company => company.AvailabilitySlots.Any(slot =>
                (date == null || slot.LocalDay == date)
                && (fromMinute == null || slot.LocalMinute >= fromMinute)
                && (toMinute == null || slot.LocalMinute <= toMinute)));
        }

        return query.Where(company => company.AvailabilitySlots.Any(slot =>
            (date == null || slot.LocalDay == date)
            && (fromMinute == null || slot.LocalMinute >= fromMinute)
            && (toMinute == null || slot.LocalMinute <= toMinute)
            // The service has to fit the gap and be one this staff member performs — which is the
            // whole reason max_duration_minutes is stored rather than a row per service.
            && company.ServiceOfferings.Any(offering =>
                offering.StaffMemberId == slot.StaffMemberId
                && slot.MaxDurationMinutes >= offering.RequiredMinutes
                && (serviceIds.Length == 0 || serviceIds.Contains(offering.ServiceId))
                && (categoryIds.Length == 0
                    || (offering.CategoryId != null && categoryIds.Contains(offering.CategoryId.Value))))));
    }

    /// <summary>Each company's first photo in display order. Photos are required-owner and the browse has no company scope.</summary>
    private static async Task<Dictionary<Guid, string>> ReadMainPhotoKeysAsync(
        IRepository<CompanyPhoto, BookingContext> companyPhotos,
        IUserContextScope userContext,
        List<Guid> companyIds,
        CancellationToken cancellationToken)
    {
        using var _ = userContext.BecomeAllOwners();

        var firstPhotos = await companyPhotos
            .QuerySelect(
                photo => new { photo.OwnerId, photo.StorageKey },
                query => query
                    .Where(photo => companyIds.Contains(photo.OwnerId)
                                    && !query.Any(other =>
                                        other.OwnerId == photo.OwnerId && other.DisplayOrder < photo.DisplayOrder))
                    .OrderBy(photo => photo.OwnerId)
                    .ThenBy(photo => photo.Id))
            .ToListAsync(cancellationToken);

        // Ties keep the SQL id order, as the shop page does.
        return firstPhotos
            .GroupBy(photo => photo.OwnerId)
            .ToDictionary(photos => photos.Key, photos => photos.First().StorageKey);
    }

    private static async Task<Guid?> ReadLanguageIdAsync(
        IRepository<Language, BookingContext> languages,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var code = languageCode.Trim().ToLowerInvariant();

        return await languages
            .QuerySelect(language => (Guid?)language.Id, query => query.Where(language => language.Code == code))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
