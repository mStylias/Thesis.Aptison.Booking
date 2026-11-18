using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.Readers;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Services.Queries;

/// <summary>What the services page reads before its grid: currency, languages, categories and staff.</summary>
public class GetServiceOptionsRequest : IQuery<Result<ServiceOptionsResponse, HttpError>>
{
    /// <summary>The request's resolved company, never bound from a caller-supplied value.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>The language of the category names. Falls back to whichever each row has.</summary>
    public string? LanguageCode { get; set; }
}

public class GetServiceOptionsHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<GetServiceOptionsRequest, Result<ServiceOptionsResponse, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();

    public async Task<Result<ServiceOptionsResponse, HttpError>> HandleAsync(
        GetServiceOptionsRequest request, CancellationToken cancellationToken)
    {
        var currency = await _companyRepo
            .QuerySelect(company => company.Currency, query => query.Where(company => company.Id == request.CompanyId))
            .FirstOrDefaultAsync(cancellationToken);

        if (currency is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var languageCodes = await _languageRepo
            .QuerySelect(language => language.Code, query => query.Where(language => language.IsActive).OrderBy(language => language.Code))
            .ToListAsync(cancellationToken);

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, request.LanguageCode, cancellationToken);

        var categories = await ServiceCategoryReader.ReadVisibleAsync(
            unitOfWork, request.CompanyId, languageId, filter: null, cancellationToken);

        return new ServiceOptionsResponse(
            currency, languageCodes, categories, await ReadStaffMembersAsync(cancellationToken));
    }

    /// <summary>Everyone on the staff: an inactive or non-bookable member may still hold an assignment.</summary>
    private async Task<List<ServiceOptionStaffMemberViewDto>> ReadStaffMembersAsync(CancellationToken cancellationToken)
    {
        var members = await _staffMemberRepo
            .QuerySelect(member => new
            {
                member.Id, member.User.FirstName, member.User.LastName, member.IsActive, member.IsBookable
            })
            .ToListAsync(cancellationToken);

        // The same order as the calendar's staff list.
        return [.. members
            .OrderBy(member => StringUtils.Fold(member.FirstName), StringComparer.Ordinal)
            .ThenBy(member => StringUtils.Fold(member.LastName), StringComparer.Ordinal)
            .ThenBy(member => member.Id)
            .Select(member => new ServiceOptionStaffMemberViewDto(
                member.Id,
                StaffMemberNames.GetDisplayName(member.FirstName, member.LastName),
                member.IsActive,
                member.IsBookable))];
    }
}
