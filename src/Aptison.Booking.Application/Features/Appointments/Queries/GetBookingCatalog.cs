using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>What a staff create or edit can choose from.</summary>
public class GetBookingCatalogRequest : IQuery<Result<AppointmentCatalogResponse, HttpError>>
{
    /// <summary>The request's resolved company, never bound from a caller-supplied value.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>The language of the names. Falls back to whichever each row has.</summary>
    public string? LanguageCode { get; set; }
}

/// <summary>
/// Prices and lengths come from <c>company_service_offerings</c>, which is what a staff write
/// charges; the live staff and service reads drop what the index has not caught up with yet.
/// </summary>
public class GetBookingCatalogHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<GetBookingCatalogRequest, Result<AppointmentCatalogResponse, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceTranslation, BookingContext> _serviceTranslationRepo =
        unitOfWork.GetRepository<ServiceTranslation>();
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<ServiceCategoryTranslation, BookingContext> _serviceCategoryTranslationRepo =
        unitOfWork.GetRepository<ServiceCategoryTranslation>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public async Task<Result<AppointmentCatalogResponse, HttpError>> HandleAsync(
        GetBookingCatalogRequest request, CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, request.LanguageCode, cancellationToken);

        var bookableStaffIds = await _staffMemberRepo
            .QuerySelect(member => member.Id, query => query.Where(member => member.IsActive && member.IsBookable))
            .ToHashSetAsync(cancellationToken);

        var indexedOfferings = await _offeringRepo
            .QuerySelect(
                offering => new { offering.StaffMemberId, offering.ServiceId, offering.Price, offering.DurationMinutes },
                query => query.Where(offering => offering.CompanyId == company.Id))
            .ToListAsync(cancellationToken);

        var offeredServiceIds = indexedOfferings.Select(offering => offering.ServiceId).Distinct().ToList();

        var services = await _serviceRepo
            .QuerySelect(
                service => new { service.Id, service.CategoryId },
                query => query.Where(service => service.IsActive && offeredServiceIds.Contains(service.Id)))
            .ToListAsync(cancellationToken);

        var serviceNamesByIds = await AppointmentViewReader.ReadServiceNamesAsync(
            _serviceTranslationRepo, [.. services.Select(service => service.Id)], languageId, cancellationToken);

        // A service with no name to show is left out, as on the booking page.
        var listedServices = services.Where(service => serviceNamesByIds.ContainsKey(service.Id)).ToList();
        var listedServiceIds = listedServices.Select(service => service.Id).ToHashSet();

        var offerings = indexedOfferings
            .Where(offering => bookableStaffIds.Contains(offering.StaffMemberId)
                               && listedServiceIds.Contains(offering.ServiceId))
            .ToList();

        var performedServiceIds = offerings.Select(offering => offering.ServiceId).ToHashSet();

        var categories = await ReadCategoriesAsync(
            [.. listedServices.Where(service => service.CategoryId is not null).Select(service => service.CategoryId!.Value).Distinct()],
            languageId,
            cancellationToken);

        var servicesByCategoryIds = listedServices
            .Where(service => performedServiceIds.Contains(service.Id))
            .OrderBy(service => StringUtils.Fold(serviceNamesByIds[service.Id]), StringComparer.Ordinal)
            .ThenBy(service => service.Id)
            .ToLookup(
                service => service.CategoryId is { } categoryId && categories.ContainsKey(categoryId)
                    ? categoryId
                    : (Guid?)null,
                service => new CatalogServiceViewDto(service.Id, serviceNamesByIds[service.Id]));

        return new AppointmentCatalogResponse(
            company.Currency,
            company.SlotIntervalMinutes,
            [.. categories.Values
                .Where(category => servicesByCategoryIds.Contains(category.Id))
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Id)
                .Select(category => new CatalogServiceCategoryViewDto(
                    category.Id, category.Name, [.. servicesByCategoryIds[category.Id]]))],
            [.. servicesByCategoryIds[null]],
            [.. offerings
                .GroupBy(offering => offering.StaffMemberId)
                .OrderBy(staff => staff.Key)
                .Select(staff => new CatalogStaffMemberViewDto(
                    staff.Key,
                    [.. staff
                        .OrderBy(offering => offering.ServiceId)
                        .Select(offering => new StaffServiceOfferingViewDto(
                            offering.ServiceId, offering.Price, offering.DurationMinutes))]))]);
    }

    /// <summary>Active categories only; a category with no name to show is treated as none.</summary>
    private async Task<Dictionary<Guid, CatalogCategory>> ReadCategoriesAsync(
        List<Guid> categoryIds, Guid? languageId, CancellationToken cancellationToken)
    {
        var activeCategories = await _serviceCategoryRepo
            .QuerySelect(
                category => new { category.Id, category.SortOrder },
                query => query.Where(category => category.IsActive && categoryIds.Contains(category.Id)))
            .ToListAsync(cancellationToken);

        var activeCategoryIds = activeCategories.Select(category => category.Id).ToList();

        var translations = await _serviceCategoryTranslationRepo
            .QuerySelect(
                translation => new { translation.ServiceCategoryId, translation.LanguageId, translation.Name },
                query => query.Where(translation => activeCategoryIds.Contains(translation.ServiceCategoryId)))
            .ToListAsync(cancellationToken);

        // Ordered by language id when the requested one is missing, as the booking endpoints do.
        var namesByCategoryIds = translations
            .GroupBy(translation => translation.ServiceCategoryId)
            .ToDictionary(
                category => category.Key,
                category => (category.FirstOrDefault(translation => translation.LanguageId == languageId)
                             ?? category.OrderBy(translation => translation.LanguageId).First()).Name);

        return activeCategories
            .Where(category => namesByCategoryIds.ContainsKey(category.Id))
            .ToDictionary(
                category => category.Id,
                category => new CatalogCategory(category.Id, category.SortOrder, namesByCategoryIds[category.Id]));
    }

    private sealed record CatalogCategory(Guid Id, int SortOrder, string Name);
}
