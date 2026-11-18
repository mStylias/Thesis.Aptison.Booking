using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries;

/// <summary>A barbershop's public home page, found by its slug.</summary>
public class GetCompanyDetailsRequest : IQuery<Result<CompanyDetailsResponse, HttpError>>
{
    /// <summary>Matched exactly, case included.</summary>
    public string Slug { get; set; } = null!;

    /// <summary>Required. Falls back to whichever translation each row has.</summary>
    public string? LanguageCode { get; set; }
}

/// <summary>
/// The slug lookup is never cached, so a new shop is found at once and a deactivated one is a 404
/// at once. Only the composed page is cached, per company and language.
/// </summary>
public class GetCompanyDetailsHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    HybridCache cache,
    IOptions<CompanyBrowseOptions> browseOptions,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : IQueryHandler<GetCompanyDetailsRequest, Result<CompanyDetailsResponse, HttpError>>,
        IWithValidation<GetCompanyDetailsRequest, Result<Guid, HttpError>>
{
    private const int MaxPhotos = 50;
    private const int MinReviewsForAverage = 5;

    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyTranslation, BookingContext> _companyTranslationRepo =
        unitOfWork.GetRepository<CompanyTranslation>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceTranslation, BookingContext> _serviceTranslationRepo =
        unitOfWork.GetRepository<ServiceTranslation>();
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<ServiceCategoryTranslation, BookingContext> _serviceCategoryTranslationRepo =
        unitOfWork.GetRepository<ServiceCategoryTranslation>();
    private readonly IRepository<CompanyPhoto, BookingContext> _companyPhotoRepo =
        unitOfWork.GetRepository<CompanyPhoto>();
    private readonly IRepository<CompanyPhotoTranslation, BookingContext> _companyPhotoTranslationRepo =
        unitOfWork.GetRepository<CompanyPhotoTranslation>();
    private readonly IRepository<Review, BookingContext> _reviewRepo = unitOfWork.GetRepository<Review>();

    private readonly CompanyBrowseOptions _browseOptions = browseOptions.Value;
    private readonly FileStorageOptions _fileStorageOptions = fileStorageOptions.Value;

    /// <summary>Resolves the language to its id, which the cache key and the fallback both use.</summary>
    public async Task<Result<Guid, HttpError>> ValidateAsync(
        GetCompanyDetailsRequest request, CancellationToken cancellationToken)
    {
        var languageCode = StringUtils.TrimToNull(request.LanguageCode)?.ToLowerInvariant();

        var languageId = languageCode is null
            ? null
            : await _languageRepo
                .QuerySelect(
                    language => (Guid?)language.Id,
                    query => query.Where(language => language.IsActive && language.Code == languageCode))
                .FirstOrDefaultAsync(cancellationToken);

        return new Validation("The request is invalid.")
            .Require(
                languageId is not null,
                ErrorCodes.Companies.InvalidLanguage,
                "The language code must be a supported language, such as 'el' or 'en'.",
                nameof(request.LanguageCode))
            .ToResult(languageId.GetValueOrDefault());
    }

    public async Task<Result<CompanyDetailsResponse, HttpError>> HandleAsync(
        GetCompanyDetailsRequest request, CancellationToken cancellationToken)
    {
        var languageId = this.ValidationData();

        var read = await Result.TryAsync(
            () => ReadAsync(request.Slug, languageId, cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch the barbershop.", errorCode: ErrorCodes.Companies.DetailsFetchFailed));

        if (read.IsError)
        {
            return read.Error!;
        }

        return read.Data is { } details
            ? details
            : HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
    }

    private async Task<CompanyDetailsResponse?> ReadAsync(
        string slug, Guid languageId, CancellationToken cancellationToken)
    {
        var companyId = await _companyRepo
            .QuerySelect(
                company => (Guid?)company.Id,
                query => query.Where(company => company.Slug == slug && company.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        if (companyId is null)
        {
            return null;
        }

        return await cache.GetOrCreateAsync(
            // Versioned: a shape change must not read a payload the previous one wrote.
            $"companies:details:v1:{companyId}:{languageId}",
            (Handler: this, CompanyId: companyId.Value, LanguageId: languageId),
            static (state, ct) => state.Handler.ComposeAsync(state.CompanyId, state.LanguageId, ct),
            new HybridCacheEntryOptions
            {
                Expiration = _browseOptions.PageCacheTtl,
                LocalCacheExpiration = _browseOptions.PageCacheLocalTtl
            },
            [CompanyCacheTags.GetCompanyTag(companyId.Value)],
            cancellationToken);
    }

    /// <summary>
    /// Null when the company has no translation at all, since it then has no name to show — the
    /// same rule the list applies.
    /// </summary>
    private async ValueTask<CompanyDetailsResponse?> ComposeAsync(
        Guid companyId, Guid languageId, CancellationToken cancellationToken)
    {
        var company = await _companyRepo
            .QuerySelect(
                candidate => new
                {
                    candidate.Id,
                    candidate.Slug,
                    candidate.PostalCode,
                    candidate.CountryCode,
                    candidate.Location,
                    candidate.Timezone,
                    candidate.Currency,
                    candidate.LogoStorageKey
                },
                query => query.Where(candidate => candidate.Id == companyId))
            .FirstOrDefaultAsync(cancellationToken);

        var companyTranslations = await _companyTranslationRepo
            .QuerySelect(
                translation => new
                {
                    translation.LanguageId,
                    translation.Name,
                    translation.Description,
                    translation.Street,
                    translation.Neighborhood,
                    translation.City,
                    translation.Region
                },
                query => query.Where(translation => translation.CompanyId == companyId))
            .ToListAsync(cancellationToken);

        if (company is null || companyTranslations.Count == 0)
        {
            return null;
        }

        var companyTranslation = ChooseTranslation(companyTranslations, t => t.LanguageId, languageId);

        // Staff, services, photos and reviews are required-owner and this endpoint has no company scope, so
        // without this every list is silently empty. Each read below filters on the company itself.
        using var _ = userContext.BecomeAllOwners();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var barbers = await _staffMemberRepo
            .QuerySelect(
                member => new { member.Id, member.User.FirstName, member.User.LastName, member.AvatarStorageKey },
                query => query.Where(member =>
                    member.OwnerId == companyId
                    && member.IsActive
                    && member.IsBookable
                    && !member.User.IsDeleted
                    && (!member.User.IsBanned || (member.User.BannedUntil != null && member.User.BannedUntil <= utcNow))))
            .ToListAsync(cancellationToken);

        var barberIds = barbers.Select(barber => barber.Id).ToList();

        // A service nobody here can perform cannot be booked, so it is not advertised.
        var offeredServiceIds = await _staffServiceRepo
            .QuerySelect(
                staffService => staffService.ServiceId,
                query => query.Where(staffService =>
                    staffService.OwnerId == companyId && barberIds.Contains(staffService.StaffMemberId)))
            .ToListAsync(cancellationToken);

        var services = await _serviceRepo
            .QuerySelect(
                service => new { service.Id, service.CategoryId, service.Price, service.DurationMinutes },
                query => query.Where(service =>
                    service.OwnerId == companyId && service.IsActive && offeredServiceIds.Contains(service.Id)))
            .ToListAsync(cancellationToken);

        var categoryIds = services
            .Where(service => service.CategoryId is not null)
            .Select(service => service.CategoryId!.Value)
            .Distinct()
            .ToList();

        var categories = await _serviceCategoryRepo
            .QuerySelect(
                category => new { category.Id, category.SortOrder },
                query => query.Where(category => categoryIds.Contains(category.Id) && category.IsActive))
            .ToListAsync(cancellationToken);

        var photos = await _companyPhotoRepo
            .QuerySelect(
                photo => new { photo.Id, photo.StorageKey, photo.Width, photo.Height },
                query => query
                    .Where(photo => photo.OwnerId == companyId)
                    .OrderBy(photo => photo.DisplayOrder)
                    .ThenBy(photo => photo.Id)
                    .Take(MaxPhotos))
            .ToListAsync(cancellationToken);

        var publishedRatings = _reviewRepo.QuerySelect(
            review => (int)review.Rating,
            query => query.Where(review => review.OwnerId == companyId && review.IsPublished));

        var reviewCount = await publishedRatings.CountAsync(cancellationToken);
        var ratingSum = reviewCount == 0 ? 0 : await publishedRatings.SumAsync(rating => rating, cancellationToken);

        var serviceIds = services.Select(service => service.Id).ToList();
        var serviceTranslations = await _serviceTranslationRepo
            .QuerySelect(
                translation => new { translation.ServiceId, translation.LanguageId, translation.Name },
                query => query.Where(translation => serviceIds.Contains(translation.ServiceId)))
            .ToListAsync(cancellationToken);

        var categoryTranslations = await _serviceCategoryTranslationRepo
            .QuerySelect(
                translation => new { translation.ServiceCategoryId, translation.LanguageId, translation.Name },
                query => query.Where(translation => categoryIds.Contains(translation.ServiceCategoryId)))
            .ToListAsync(cancellationToken);

        var photoIds = photos.Select(photo => photo.Id).ToList();
        var photoTranslations = await _companyPhotoTranslationRepo
            .QuerySelect(
                translation => new { translation.PhotoId, translation.AltText },
                // No other language's text: a language without its own falls back to the shop's name.
                query => query.Where(translation =>
                    photoIds.Contains(translation.PhotoId) && translation.LanguageId == languageId))
            .ToListAsync(cancellationToken);

        var serviceNamesByIds = serviceTranslations
            .GroupBy(translation => translation.ServiceId)
            .ToDictionary(
                service => service.Key,
                service => ChooseTranslation(service, t => t.LanguageId, languageId).Name);

        var categoryNamesByIds = categoryTranslations
            .GroupBy(translation => translation.ServiceCategoryId)
            .ToDictionary(
                category => category.Key,
                category => ChooseTranslation(category, t => t.LanguageId, languageId).Name);

        var altTextsByPhotoIds = photoTranslations.ToDictionary(
            translation => translation.PhotoId, translation => translation.AltText);

        // A category with no name to show is treated as no category, so its services still appear.
        var listedCategoryIds = categories
            .Where(category => categoryNamesByIds.ContainsKey(category.Id))
            .Select(category => category.Id)
            .ToHashSet();

        var servicesByCategoryIds = services
            .Where(service => serviceNamesByIds.ContainsKey(service.Id))
            .ToLookup(
                service => service.CategoryId is { } categoryId && listedCategoryIds.Contains(categoryId)
                    ? categoryId
                    : (Guid?)null,
                service => new CompanyServiceViewDto(
                    service.Id, serviceNamesByIds[service.Id], service.Price, service.DurationMinutes));

        var serviceCategories = categories
            .Where(category => servicesByCategoryIds.Contains(category.Id))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Select(category => new CompanyServiceCategoryViewDto(
                category.Id, categoryNamesByIds[category.Id], SortServices(servicesByCategoryIds[category.Id])))
            .ToList();

        var photoViews = new List<CompanyPhotoViewDto>(photos.Count);

        foreach (var photo in photos)
        {
            if (FileStorageUtils.BuildPublicUrl(photo.StorageKey, _fileStorageOptions.PublicBaseUrl) is not { } url)
            {
                continue;
            }

            photoViews.Add(new CompanyPhotoViewDto(
                url,
                photo.Width,
                photo.Height,
                altTextsByPhotoIds.GetValueOrDefault(photo.Id) ?? companyTranslation.Name));
        }

        var barberViews = barbers
            .OrderBy(barber => StringUtils.Fold(barber.FirstName), StringComparer.Ordinal)
            .ThenBy(barber => StringUtils.Fold(barber.LastName), StringComparer.Ordinal)
            .ThenBy(barber => barber.Id)
            .Select(barber => new CompanyStaffMemberViewDto(
                barber.Id,
                StaffMemberNames.GetDisplayName(barber.FirstName, barber.LastName),
                StaffMemberNames.GetInitials(barber.FirstName, barber.LastName),
                FileStorageUtils.BuildPublicUrl(barber.AvatarStorageKey, _fileStorageOptions.PublicBaseUrl)))
            .ToList();

        var rating = new CompanyRatingViewDto(
            reviewCount,
            reviewCount >= MinReviewsForAverage
                ? Math.Round((decimal)ratingSum / reviewCount, 1, MidpointRounding.AwayFromZero)
                : null);

        return new CompanyDetailsResponse(
            company.Id,
            company.Slug,
            companyTranslation.Name,
            companyTranslation.Description,
            companyTranslation.Street,
            companyTranslation.Neighborhood,
            companyTranslation.City,
            companyTranslation.Region,
            company.PostalCode,
            company.CountryCode,
            company.Location.Y,
            company.Location.X,
            company.Timezone,
            company.Currency,
            FileStorageUtils.BuildPublicUrl(company.LogoStorageKey, _fileStorageOptions.PublicBaseUrl),
            photoViews,
            rating,
            serviceCategories,
            SortServices(servicesByCategoryIds[null]),
            barberViews);
    }

    // Ordered by language id when the requested one is missing, so every request agrees on the fallback.
    private static T ChooseTranslation<T>(IEnumerable<T> translations, Func<T, Guid> getLanguageId, Guid languageId)
        where T : class =>
        translations.FirstOrDefault(translation => getLanguageId(translation) == languageId)
        ?? translations.OrderBy(getLanguageId).First();

    private static List<CompanyServiceViewDto> SortServices(IEnumerable<CompanyServiceViewDto> services) =>
        [.. services
            .OrderBy(service => StringUtils.Fold(service.Name), StringComparer.Ordinal)
            .ThenBy(service => service.Id)];
}
