using System.Linq.Expressions;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Companies.Common.ResponseOData;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries.OData;

/// <summary>Every company that is not deleted, active or not, for the superadmin's shop picker.</summary>
public class ListPlatformCompaniesODataRequest : IQuery<Result<ISynaxQuery<CompanyODataResponse>, HttpError>>
{
    /// <summary>The language of the name. Falls back to whichever each company has.</summary>
    public string? LanguageCode { get; set; }
}

public class ListPlatformCompaniesODataHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<ListPlatformCompaniesODataRequest, Result<ISynaxQuery<CompanyODataResponse>, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public async Task<Result<ISynaxQuery<CompanyODataResponse>, HttpError>> HandleAsync(
        ListPlatformCompaniesODataRequest request, CancellationToken cancellationToken)
    {
        var languageCode = StringUtils.TrimToNull(request.LanguageCode)?.ToLowerInvariant();

        var languageId = languageCode is null
            ? null
            : await _languageRepo
                .QuerySelect(
                    language => (Guid?)language.Id,
                    query => query.Where(language => language.IsActive && language.Code == languageCode))
                .FirstOrDefaultAsync(cancellationToken);

        return Result.Ok<ISynaxQuery<CompanyODataResponse>, HttpError>(
            _companyRepo.QuerySelect(BuildProjection(languageId)));
    }

    /// <summary>
    /// Written here rather than as a profile, because the name follows the requested language and a
    /// profile is built once at startup.
    /// </summary>
    private static Expression<Func<Company, CompanyODataResponse>> BuildProjection(Guid? languageId) =>
        company => new CompanyODataResponse
        {
            Id = company.Id,
            Name = company.Translations
                .OrderBy(translation => translation.LanguageId == languageId ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.Name)
                .FirstOrDefault() ?? "",
            Slug = company.Slug,
            IsActive = company.IsActive
        };
}
