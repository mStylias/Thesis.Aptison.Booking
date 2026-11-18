using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common;
using Aptison.Booking.Application.Features.Analytics.Common.Charts;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Domain;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Analytics.Queries;

/// <summary>Whether people come back.</summary>
public class GetCustomersTabRequest : AnalyticsRequest, IQuery<Result<CustomersTabResponse, HttpError>>;

public class GetCustomersTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetCustomersTabRequest, CustomersTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<CustomersTabResponse> BuildAsync(
        GetCustomersTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            NewVsReturning = await CustomerCharts.BuildNewVsReturningAsync(facts, cancellationToken),
            DaysBetweenVisits = await CustomerCharts.BuildDaysBetweenVisitsAsync(facts, cancellationToken),
            TopCustomers = await CustomerCharts.BuildTopCustomersAsync(facts, cancellationToken)
        };
}
