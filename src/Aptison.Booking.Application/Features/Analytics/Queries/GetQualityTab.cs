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

/// <summary>Whether customers are happy.</summary>
public class GetQualityTabRequest : AnalyticsRequest, IQuery<Result<QualityTabResponse, HttpError>>;

public class GetQualityTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetQualityTabRequest, QualityTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<QualityTabResponse> BuildAsync(
        GetQualityTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            RatingOverTime = await QualityCharts.BuildRatingOverTimeAsync(facts, cancellationToken),
            RatingDistribution = await QualityCharts.BuildRatingDistributionAsync(facts, cancellationToken)
        };
}
