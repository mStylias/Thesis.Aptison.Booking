using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries;

/// <summary>
/// Current and upcoming holidays, soonest first; with <c>Past</c>, ended ones, latest first and at
/// most <see cref="ListCompanyHolidaysHandler.MaxPast"/>.
/// </summary>
public class ListCompanyHolidaysRequest : IQuery<Result<IReadOnlyList<CompanyHolidayResponse>, HttpError>>
{
    /// <summary>Null when the caller works at no shop.</summary>
    public Guid? CompanyId { get; set; }

    public bool Past { get; set; }
}

public class ListCompanyHolidaysHandler(IUnitOfWork<BookingContext> unitOfWork, IMapper mapper, TimeProvider timeProvider)
    : IQueryHandler<ListCompanyHolidaysRequest, Result<IReadOnlyList<CompanyHolidayResponse>, HttpError>>
{
    public const int MaxPast = 100;

    private readonly IRepository<CompanyHoliday, BookingContext> _holidayRepo = unitOfWork.GetRepository<CompanyHoliday>();

    public async Task<Result<IReadOnlyList<CompanyHolidayResponse>, HttpError>> HandleAsync(
        ListCompanyHolidaysRequest request, CancellationToken cancellationToken)
    {
        if (request.CompanyId is not { } companyId)
        {
            return CompanySettingsErrors.CreateScopeMissing();
        }

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), companyId, cancellationToken);

        if (company is null)
        {
            return CompanySettingsErrors.CreateNotFound();
        }

        var today = DateOnly.FromDateTime(ShopTime.ToLocal(
            timeProvider.GetUtcNow().UtcDateTime, BookingCompanyReader.GetTimeZone(company.Timezone)));

        var holidays = await _holidayRepo
            .Query(query => request.Past
                ? query
                    .Where(holiday => holiday.EndsOn < today)
                    .OrderByDescending(holiday => holiday.EndsOn)
                    .ThenByDescending(holiday => holiday.StartsOn)
                    .Take(MaxPast)
                : query
                    .Where(holiday => holiday.EndsOn >= today)
                    .OrderBy(holiday => holiday.StartsOn))
            .ToListAsync(cancellationToken);

        return Result.Ok<IReadOnlyList<CompanyHolidayResponse>, HttpError>(
            [.. holidays.Select(holiday => mapper.Map<CompanyHolidayResponse>(holiday))]);
    }
}
