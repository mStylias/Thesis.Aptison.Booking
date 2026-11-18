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

/// <summary>How the shop is doing: the six KPIs.</summary>
public class GetOverviewTabRequest : AnalyticsRequest, IQuery<Result<OverviewTabResponse, HttpError>>;

public class GetOverviewTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetOverviewTabRequest, OverviewTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<OverviewTabResponse> BuildAsync(
        GetOverviewTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            Revenue = await OverviewKpis.BuildRevenueAsync(facts, cancellationToken),
            Appointments = await OverviewKpis.BuildAppointmentsAsync(facts, cancellationToken),
            AverageTicket = await OverviewKpis.BuildAverageTicketAsync(facts, cancellationToken),
            UniqueCustomers = await OverviewKpis.BuildUniqueCustomersAsync(facts, cancellationToken),
            LostRate = await OverviewKpis.BuildLostRateAsync(facts, cancellationToken),
            AverageRating = await OverviewKpis.BuildAverageRatingAsync(facts, cancellationToken)
        };
}
