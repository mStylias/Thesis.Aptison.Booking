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

/// <summary>Where the money comes from.</summary>
public class GetRevenueTabRequest : AnalyticsRequest, IQuery<Result<RevenueTabResponse, HttpError>>;

public class GetRevenueTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetRevenueTabRequest, RevenueTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<RevenueTabResponse> BuildAsync(
        GetRevenueTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            RevenueOverTime = await RevenueCharts.BuildRevenueOverTimeAsync(facts, cancellationToken),
            RevenuePerStaff = await RevenueCharts.BuildRevenuePerStaffAsync(facts, cancellationToken),
            RevenuePerService = await ServiceCharts.BuildRevenuePerServiceAsync(facts, cancellationToken)
        };
}
