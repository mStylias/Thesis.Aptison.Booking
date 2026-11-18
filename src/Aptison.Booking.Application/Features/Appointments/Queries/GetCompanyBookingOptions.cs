using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain;
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

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>Everything the booking stepper renders before it asks for a day, found by slug.</summary>
public class GetCompanyBookingOptionsRequest : IQuery<Result<CompanyBookingOptionsResponse, HttpError>>
{
    public string Slug { get; set; } = null!;

    /// <summary>Required. Falls back to whichever translation each row has.</summary>
    public string? LanguageCode { get; set; }
}

/// <summary>
/// Cached per company and language, like the home page, and evicted by the same tag. Nothing here
/// depends on the calendar — availability is the two endpoints beside this one.
/// </summary>
public class GetCompanyBookingOptionsHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    HybridCache cache,
    IOptions<CompanyBrowseOptions> browseOptions,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : IQueryHandler<GetCompanyBookingOptionsRequest, Result<CompanyBookingOptionsResponse, HttpError>>,
        IWithValidation<GetCompanyBookingOptionsRequest, Result<Guid, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyTranslation, BookingContext> _companyTranslationRepo =
        unitOfWork.GetRepository<CompanyTranslation>();
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceTranslation, BookingContext> _serviceTranslationRepo =
        unitOfWork.GetRepository<ServiceTranslation>();
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<ServiceCategoryTranslation, BookingContext> _serviceCategoryTranslationRepo =
        unitOfWork.GetRepository<ServiceCategoryTranslation>();

    private readonly CompanyBrowseOptions _browseOptions = browseOptions.Value;
    private readonly FileStorageOptions _fileStorageOptions = fileStorageOptions.Value;

    public async Task<Result<Guid, HttpError>> ValidateAsync(
        GetCompanyBookingOptionsRequest request, CancellationToken cancellationToken)
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

    public async Task<Result<CompanyBookingOptionsResponse, HttpError>> HandleAsync(
        GetCompanyBookingOptionsRequest request, CancellationToken cancellationToken)
    {
        var languageId = this.ValidationData();

        var read = await Result.TryAsync(
            () => ReadAsync(request.Slug, languageId, cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch the booking options.",
                errorCode: ErrorCodes.Appointments.BookingOptionsFetchFailed));

        if (read.IsError)
        {
            return read.Error!;
        }

        return read.Data is { } options
            ? options
            : HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
    }

    private async Task<CompanyBookingOptionsResponse?> ReadAsync(
        string slug, Guid languageId, CancellationToken cancellationToken)
    {
        // The slug lookup stays uncached, so a new shop is bookable at once and a deactivated one
        // is a 404 at once. Only the composed page is cached.
        var company = await BookingCompanyReader.ReadBySlugAsync(_companyRepo, slug, cancellationToken);

        if (company is null)
        {
            return null;
        }

        return await cache.GetOrCreateAsync(
            // Versioned: a shape change must not read a payload the previous one wrote.
            $"companies:booking-options:v1:{company.Id}:{languageId}",
            (Handler: this, Company: company, LanguageId: languageId),
            static (state, ct) => state.Handler.ComposeAsync(state.Company, state.LanguageId, ct),
            new HybridCacheEntryOptions
            {
                Expiration = _browseOptions.PageCacheTtl,
                LocalCacheExpiration = _browseOptions.PageCacheLocalTtl
            },
            [CompanyCacheTags.GetCompanyTag(company.Id)],
            cancellationToken);
    }

    /// <summary>Null when the shop has no translation at all, since it then has no name to show.</summary>
    private async ValueTask<CompanyBookingOptionsResponse?> ComposeAsync(
        BookingCompanyDto company, Guid languageId, CancellationToken cancellationToken)
    {
        var companyTranslations = await _companyTranslationRepo
            .QuerySelect(
                translation => new { translation.LanguageId, translation.Name },
                query => query.Where(translation => translation.CompanyId == company.Id))
            .ToListAsync(cancellationToken);

        if (companyTranslations.Count == 0)
        {
            return null;
        }

        var companyName = ChooseTranslation(companyTranslations, t => t.LanguageId, languageId).Name;

        var offerings = await _offeringRepo
            .QuerySelect(
                offering => new
                {
                    offering.StaffMemberId,
                    offering.ServiceId,
                    offering.CategoryId,
                    offering.Price,
                    offering.DurationMinutes
                },
                query => query.Where(offering => offering.CompanyId == company.Id))
            .ToListAsync(cancellationToken);

        if (offerings.Count == 0)
        {
            return BuildEmpty(company, companyName);
        }

        // Staff, services and company categories are required-owner and this endpoint has no
        // company scope, so without this every list below is silently empty.
        using var _ = userContext.BecomeAllOwners();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var barbers = await _staffMemberRepo
            .QuerySelect(
                member => new { member.Id, member.User.FirstName, member.User.LastName, member.AvatarStorageKey },
                query => query.Where(member =>
                    member.OwnerId == company.Id
                    && member.IsActive
                    && member.IsBookable
                    && !member.User.IsDeleted
                    && (!member.User.IsBanned || (member.User.BannedUntil != null && member.User.BannedUntil <= utcNow))))
            .ToListAsync(cancellationToken);

        // The offerings index is rebuilt asynchronously, so it can still name a barber who was
        // just deactivated or banned. The live read above is the arbiter.
        var barberIds = barbers.Select(barber => barber.Id).ToHashSet();
        var liveOfferings = offerings.Where(offering => barberIds.Contains(offering.StaffMemberId)).ToList();

        var offeredServiceIds = liveOfferings.Select(offering => offering.ServiceId).ToHashSet();

        var services = await _serviceRepo
            .QuerySelect(
                service => new { service.Id, service.CategoryId, service.Price, service.DurationMinutes },
                query => query.Where(service =>
                    service.OwnerId == company.Id && service.IsActive && offeredServiceIds.Contains(service.Id)))
            .ToListAsync(cancellationToken);

        var serviceIds = services.Select(service => service.Id).ToList();

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

        var serviceTranslations = await _serviceTranslationRepo
            .QuerySelect(
                translation => new
                {
                    translation.ServiceId, translation.LanguageId, translation.Name, translation.Description
                },
                query => query.Where(translation => serviceIds.Contains(translation.ServiceId)))
            .ToListAsync(cancellationToken);

        var categoryTranslations = await _serviceCategoryTranslationRepo
            .QuerySelect(
                translation => new { translation.ServiceCategoryId, translation.LanguageId, translation.Name },
                query => query.Where(translation => categoryIds.Contains(translation.ServiceCategoryId)))
            .ToListAsync(cancellationToken);

        var serviceTextsByIds = serviceTranslations
            .GroupBy(translation => translation.ServiceId)
            .ToDictionary(
                service => service.Key,
                service => ChooseTranslation(service, t => t.LanguageId, languageId));

        var categoryNamesByIds = categoryTranslations
            .GroupBy(translation => translation.ServiceCategoryId)
            .ToDictionary(
                category => category.Key,
                category => ChooseTranslation(category, t => t.LanguageId, languageId).Name);

        var listedServiceIds = services
            .Where(service => serviceTextsByIds.ContainsKey(service.Id))
            .Select(service => service.Id)
            .ToHashSet();

        var staffIdsByServiceIds = liveOfferings
            .Where(offering => listedServiceIds.Contains(offering.ServiceId))
            .GroupBy(offering => offering.ServiceId)
            .ToDictionary(
                service => service.Key,
                service => (IReadOnlyList<Guid>)service
                    .Select(offering => offering.StaffMemberId)
                    .Distinct()
                    .Order()
                    .ToList());

        // A category with no name to show is treated as no category, so its services still appear.
        var listedCategoryIds = categories
            .Where(category => categoryNamesByIds.ContainsKey(category.Id))
            .Select(category => category.Id)
            .ToHashSet();

        var servicesByCategoryIds = services
            .Where(service => listedServiceIds.Contains(service.Id))
            .ToLookup(
                service => service.CategoryId is { } categoryId && listedCategoryIds.Contains(categoryId)
                    ? categoryId
                    : (Guid?)null,
                service => new BookingServiceViewDto(
                    service.Id,
                    serviceTextsByIds[service.Id].Name,
                    serviceTextsByIds[service.Id].Description,
                    service.Price,
                    service.DurationMinutes,
                    staffIdsByServiceIds[service.Id]));

        var serviceCategories = categories
            .Where(category => servicesByCategoryIds.Contains(category.Id))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Select(category => new BookingServiceCategoryViewDto(
                category.Id, categoryNamesByIds[category.Id], SortServices(servicesByCategoryIds[category.Id])))
            .ToList();

        var offeringsByStaffIds = liveOfferings
            .Where(offering => listedServiceIds.Contains(offering.ServiceId))
            .ToLookup(offering => offering.StaffMemberId);

        var barberViews = barbers
            .Where(barber => offeringsByStaffIds.Contains(barber.Id))
            .OrderBy(barber => StringUtils.Fold(barber.FirstName), StringComparer.Ordinal)
            .ThenBy(barber => StringUtils.Fold(barber.LastName), StringComparer.Ordinal)
            .ThenBy(barber => barber.Id)
            .Select(barber => new BookableStaffMemberViewDto(
                barber.Id,
                StaffMemberNames.GetDisplayName(barber.FirstName, barber.LastName),
                StaffMemberNames.GetInitials(barber.FirstName, barber.LastName),
                FileStorageUtils.BuildPublicUrl(barber.AvatarStorageKey, _fileStorageOptions.PublicBaseUrl),
                [.. offeringsByStaffIds[barber.Id]
                    .Select(offering => new StaffServiceOfferingViewDto(
                        offering.ServiceId, offering.Price, offering.DurationMinutes))
                    .OrderBy(offering => offering.ServiceId)]))
            .ToList();

        return new CompanyBookingOptionsResponse(
            company.Id,
            company.Slug,
            companyName,
            FileStorageUtils.BuildPublicUrl(company.LogoStorageKey, _fileStorageOptions.PublicBaseUrl),
            company.Timezone,
            company.Currency,
            company.SlotIntervalMinutes,
            company.MinLeadTimeMinutes,
            company.MaxAdvanceDays,
            company.CancellationCutoffMinutes,
            serviceCategories,
            SortServices(servicesByCategoryIds[null]),
            barberViews);
    }

    private CompanyBookingOptionsResponse BuildEmpty(BookingCompanyDto company, string companyName) =>
        new(company.Id,
            company.Slug,
            companyName,
            FileStorageUtils.BuildPublicUrl(company.LogoStorageKey, _fileStorageOptions.PublicBaseUrl),
            company.Timezone,
            company.Currency,
            company.SlotIntervalMinutes,
            company.MinLeadTimeMinutes,
            company.MaxAdvanceDays,
            company.CancellationCutoffMinutes,
            [],
            [],
            []);

    // Ordered by language id when the requested one is missing, so every request agrees on the fallback.
    private static T ChooseTranslation<T>(IEnumerable<T> translations, Func<T, Guid> getLanguageId, Guid languageId)
        where T : class =>
        translations.FirstOrDefault(translation => getLanguageId(translation) == languageId)
        ?? translations.OrderBy(getLanguageId).First();

    private static List<BookingServiceViewDto> SortServices(IEnumerable<BookingServiceViewDto> services) =>
        [.. services
            .OrderBy(service => StringUtils.Fold(service.Name), StringComparer.Ordinal)
            .ThenBy(service => service.Id)];
}
