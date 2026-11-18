using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Domain.Reference;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Queries;

/// <summary>What the staff page reads once per visit: the shop's hours, the roles and the services.</summary>
public class GetStaffOptionsRequest : IQuery<Result<StaffOptionsResponse, HttpError>>
{
    public Guid CompanyId { get; set; }

    /// <summary>The language of the service and category names. Falls back to whichever each row has.</summary>
    public string? LanguageCode { get; set; }
}

public class GetStaffOptionsHandler(IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller)
    : IQueryHandler<GetStaffOptionsRequest, Result<StaffOptionsResponse, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyBusinessHour, BookingContext> _businessHourRepo =
        unitOfWork.GetRepository<CompanyBusinessHour>();
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceTranslation, BookingContext> _serviceTranslationRepo =
        unitOfWork.GetRepository<ServiceTranslation>();
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<ServiceCategoryTranslation, BookingContext> _serviceCategoryTranslationRepo =
        unitOfWork.GetRepository<ServiceCategoryTranslation>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public async Task<Result<StaffOptionsResponse, HttpError>> HandleAsync(
        GetStaffOptionsRequest request, CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var businessHours = await ShopScheduleReader.ReadBusinessHoursAsync(_businessHourRepo, cancellationToken);

        var roles = await _roleRepo
            .QuerySelect(
                role => new { role.Id, role.Name, role.IsSystem },
                query => RoleVisibility.Apply(query, caller)
                    .OrderBy(role => role.OwnerId != null)
                    .ThenBy(role => role.Name))
            .ToListAsync(cancellationToken);

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, request.LanguageCode, cancellationToken);

        return new StaffOptionsResponse(
            company.Timezone,
            company.SlotIntervalMinutes,
            company.Currency,
            BusinessHourRules.ToViews(businessHours.Select(hour => ((short)hour.DayOfWeek, hour.Start, hour.End))),
            StaffJobTitleCodes.All,
            [.. roles.Select(role => new StaffRoleOptionViewDto(role.Id, role.Name, role.IsSystem))],
            await ReadServicesAsync(languageId, cancellationToken));
    }

    /// <summary>Platform categories first, then the shop's, each by sort order; uncategorised last.</summary>
    private async Task<List<StaffServiceOptionViewDto>> ReadServicesAsync(
        Guid? languageId, CancellationToken cancellationToken)
    {
        var services = await _serviceRepo
            .QuerySelect(service => new
            {
                service.Id, service.CategoryId, service.Price, service.DurationMinutes, service.IsActive
            })
            .ToListAsync(cancellationToken);

        var serviceNamesByIds = await AppointmentViewReader.ReadServiceNamesAsync(
            _serviceTranslationRepo, [.. services.Select(service => service.Id)], languageId, cancellationToken);

        var categoryIds = services
            .Where(service => service.CategoryId is not null)
            .Select(service => service.CategoryId!.Value)
            .Distinct()
            .ToList();

        var categoriesByIds = await _serviceCategoryRepo
            .QuerySelect(
                category => new { category.Id, IsPlatform = category.OwnerId == null, category.SortOrder },
                query => query.Where(category => categoryIds.Contains(category.Id)))
            .ToDictionaryAsync(category => category.Id, cancellationToken);

        var categoryTranslations = await _serviceCategoryTranslationRepo
            .QuerySelect(
                translation => new { translation.ServiceCategoryId, translation.LanguageId, translation.Name },
                query => query.Where(translation => categoryIds.Contains(translation.ServiceCategoryId)))
            .ToListAsync(cancellationToken);

        // Ordered by language id when the requested one is missing, as the booking endpoints do.
        var categoryNamesByIds = categoryTranslations
            .GroupBy(translation => translation.ServiceCategoryId)
            .ToDictionary(
                category => category.Key,
                category => (category.FirstOrDefault(translation => translation.LanguageId == languageId)
                             ?? category.OrderBy(translation => translation.LanguageId).First()).Name);

        return [.. services
            .Select(service =>
            {
                var category = service.CategoryId is { } categoryId && categoriesByIds.TryGetValue(categoryId, out var found)
                    ? found
                    : null;

                return new
                {
                    Category = category,
                    View = new StaffServiceOptionViewDto(
                        service.Id,
                        serviceNamesByIds.GetValueOrDefault(service.Id) ?? string.Empty,
                        category?.Id,
                        category is null ? null : categoryNamesByIds.GetValueOrDefault(category.Id),
                        service.Price,
                        service.DurationMinutes,
                        service.IsActive)
                };
            })
            .OrderBy(option => option.Category is null)
            .ThenBy(option => option.Category is { IsPlatform: false })
            .ThenBy(option => option.Category?.SortOrder ?? 0)
            .ThenBy(option => option.Category?.Id ?? Guid.Empty)
            .ThenBy(option => StringUtils.Fold(option.View.Name), StringComparer.Ordinal)
            .ThenBy(option => option.View.Id)
            .Select(option => option.View)];
    }
}
