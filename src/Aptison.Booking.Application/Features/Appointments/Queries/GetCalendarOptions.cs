using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>What the management calendar needs before it asks for any appointment.</summary>
public class GetCalendarOptionsRequest : IQuery<Result<CalendarOptionsResponse, HttpError>>
{
    /// <summary>The request's resolved company, never bound from a caller-supplied value.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>The language of the category names. Falls back to whichever each row has.</summary>
    public string? LanguageCode { get; set; }
}

/// <summary>
/// Every read but the company's is ownership-filtered to the request's company, which is what scopes
/// them.
/// </summary>
public class GetCalendarOptionsHandler(
    IUnitOfWork<BookingContext> unitOfWork, IOptions<FileStorageOptions> fileStorageOptions)
    : IQueryHandler<GetCalendarOptionsRequest, Result<CalendarOptionsResponse, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyBusinessHour, BookingContext> _businessHourRepo =
        unitOfWork.GetRepository<CompanyBusinessHour>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<ServiceCategoryTranslation, BookingContext> _serviceCategoryTranslationRepo =
        unitOfWork.GetRepository<ServiceCategoryTranslation>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly FileStorageOptions _fileStorageOptions = fileStorageOptions.Value;

    public async Task<Result<CalendarOptionsResponse, HttpError>> HandleAsync(
        GetCalendarOptionsRequest request, CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdAsync(_companyRepo, request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, request.LanguageCode, cancellationToken);

        return new CalendarOptionsResponse(
            company.Timezone,
            company.SlotIntervalMinutes,
            await ReadBusinessHoursAsync(cancellationToken),
            await ReadStaffMembersAsync(cancellationToken),
            await ReadServiceCategoriesAsync(languageId, cancellationToken));
    }

    private async Task<List<BusinessHourViewDto>> ReadBusinessHoursAsync(CancellationToken cancellationToken)
    {
        var hours = await _businessHourRepo
            .QuerySelect(hour => new { hour.DayOfWeek, hour.OpenTime, hour.CloseTime })
            .ToListAsync(cancellationToken);

        return BusinessHourRules.ToViews(hours.Select(hour => (hour.DayOfWeek, hour.OpenTime, hour.CloseTime)));
    }

    /// <summary>Inactive staff stay listed: their appointments are still in the book.</summary>
    private async Task<List<CalendarStaffMemberViewDto>> ReadStaffMembersAsync(CancellationToken cancellationToken)
    {
        var members = await _staffMemberRepo
            .QuerySelect(
                member => new
                {
                    member.Id, member.User.FirstName, member.User.LastName, member.AvatarStorageKey, member.IsActive
                },
                query => query.Where(member => member.IsBookable))
            .ToListAsync(cancellationToken);

        // The same order as the booking page's barbers.
        return [.. members
            .OrderBy(member => StringUtils.Fold(member.FirstName), StringComparer.Ordinal)
            .ThenBy(member => StringUtils.Fold(member.LastName), StringComparer.Ordinal)
            .ThenBy(member => member.Id)
            .Select(member => new CalendarStaffMemberViewDto(
                member.Id,
                StaffMemberNames.GetDisplayName(member.FirstName, member.LastName),
                StaffMemberNames.GetInitials(member.FirstName, member.LastName),
                FileStorageUtils.BuildPublicUrl(member.AvatarStorageKey, _fileStorageOptions.PublicBaseUrl),
                member.IsActive))];
    }

    /// <summary>
    /// The active categories the company's services use, inactive services included: their past
    /// appointments still carry the category.
    /// </summary>
    private async Task<List<CalendarServiceCategoryViewDto>> ReadServiceCategoriesAsync(
        Guid? languageId, CancellationToken cancellationToken)
    {
        var usedCategoryIds = await _serviceRepo
            .QuerySelect(
                service => service.CategoryId!.Value,
                query => query.Where(service => service.CategoryId != null))
            .ToHashSetAsync(cancellationToken);

        var categories = await _serviceCategoryRepo
            .QuerySelect(
                category => new { category.Id, category.SortOrder },
                query => query.Where(category => category.IsActive && usedCategoryIds.Contains(category.Id)))
            .ToListAsync(cancellationToken);

        var categoryIds = categories.Select(category => category.Id).ToList();

        var translations = await _serviceCategoryTranslationRepo
            .QuerySelect(
                translation => new { translation.ServiceCategoryId, translation.LanguageId, translation.Name },
                query => query.Where(translation => categoryIds.Contains(translation.ServiceCategoryId)))
            .ToListAsync(cancellationToken);

        // Ordered by language id when the requested one is missing, as the booking endpoints do.
        var categoryNamesByIds = translations
            .GroupBy(translation => translation.ServiceCategoryId)
            .ToDictionary(
                category => category.Key,
                category => (category.FirstOrDefault(translation => translation.LanguageId == languageId)
                             ?? category.OrderBy(translation => translation.LanguageId).First()).Name);

        // A category with no name to show is left out, as on the booking page.
        return [.. categories
            .Where(category => categoryNamesByIds.ContainsKey(category.Id))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Select(category => new CalendarServiceCategoryViewDto(category.Id, categoryNamesByIds[category.Id]))];
    }
}
