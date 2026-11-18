using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Translations;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries;

public class GetCompanySettingsRequest : IQuery<Result<CompanySettingsResponse, HttpError>>
{
    /// <summary>Null when the caller works at no shop.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Name and address fall back to Greek, then to whichever translation the shop has.</summary>
    public string? LanguageCode { get; set; }
}

public class GetCompanySettingsHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOptions<FileStorageOptions> fileStorageOptions,
    IOptions<SupportOptions> supportOptions)
    : IQueryHandler<GetCompanySettingsRequest, Result<CompanySettingsResponse, HttpError>>
{
    private const string FallbackLanguageCode = "el";

    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyTranslation, BookingContext> _companyTranslationRepo =
        unitOfWork.GetRepository<CompanyTranslation>();
    private readonly IRepository<CompanyBusinessHour, BookingContext> _businessHourRepo =
        unitOfWork.GetRepository<CompanyBusinessHour>();

    public async Task<Result<CompanySettingsResponse, HttpError>> HandleAsync(
        GetCompanySettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.CompanyId is not { } companyId)
        {
            return CompanySettingsErrors.CreateScopeMissing();
        }

        var read = await Result.TryAsync(
            () => ReadAsync(companyId, request.LanguageCode, cancellationToken),
            _ => CompanySettingsErrors.CreateFetchFailed());

        if (read.IsError)
        {
            return read.Error!;
        }

        return read.Data is { } settings ? settings : CompanySettingsErrors.CreateNotFound();
    }

    private async Task<CompanySettingsResponse?> ReadAsync(
        Guid companyId, string? languageCode, CancellationToken cancellationToken)
    {
        // Inactive shops included: a platform administrator may open one that is switched off.
        var company = await _companyRepo
            .QuerySelect(
                candidate => new
                {
                    candidate.Id,
                    candidate.Slug,
                    candidate.PostalCode,
                    candidate.CountryCode,
                    candidate.Phone,
                    candidate.Email,
                    candidate.Website,
                    candidate.Timezone,
                    candidate.Currency,
                    candidate.LogoStorageKey
                },
                query => query.Where(candidate => candidate.Id == companyId))
            .FirstOrDefaultAsync(cancellationToken);

        var bookingRules = await _companyRepo
            .QueryDto<CompanyBookingRulesResponse>(query => query.Where(candidate => candidate.Id == companyId))
            .FirstOrDefaultAsync(cancellationToken);

        var translations = await _companyTranslationRepo
            .QuerySelect(
                translation => new
                {
                    translation.LanguageId,
                    LanguageCode = translation.Language.Code,
                    translation.Name,
                    translation.Street,
                    translation.Neighborhood,
                    translation.City,
                    translation.Region
                },
                query => query.Where(translation => translation.CompanyId == companyId))
            .ToListAsync(cancellationToken);

        if (company is null || bookingRules is null || translations.Count == 0)
        {
            return null;
        }

        var requestedCode = StringUtils.TrimToNull(languageCode)?.ToLowerInvariant();
        var translation = translations.FirstOrDefault(row => row.LanguageCode == requestedCode)
                          ?? translations.FirstOrDefault(row => row.LanguageCode == FallbackLanguageCode)
                          ?? translations.OrderBy(row => row.LanguageId).First();

        var businessHours = await ShopScheduleReader.ReadBusinessHoursAsync(_businessHourRepo, cancellationToken);

        return new CompanySettingsResponse(
            company.Id,
            company.Slug,
            translation.Name,
            translation.Street,
            translation.Neighborhood,
            translation.City,
            translation.Region,
            company.PostalCode,
            company.CountryCode,
            company.Phone,
            company.Email,
            company.Website,
            company.Timezone,
            company.Currency,
            FileStorageUtils.BuildPublicUrl(company.LogoStorageKey, fileStorageOptions.Value.PublicBaseUrl),
            BusinessHourRules.ToViews(businessHours.Select(hour => ((short)hour.DayOfWeek, hour.Start, hour.End))),
            bookingRules,
            StringUtils.TrimToNull(supportOptions.Value.Email));
    }
}
