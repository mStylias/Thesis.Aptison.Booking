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

/// <summary>What the shop is missing.</summary>
public class GetLostBusinessTabRequest : AnalyticsRequest, IQuery<Result<LostBusinessTabResponse, HttpError>>;

public class GetLostBusinessTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetLostBusinessTabRequest, LostBusinessTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<LostBusinessTabResponse> BuildAsync(
        GetLostBusinessTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            StatusMix = await LostBusinessCharts.BuildStatusMixAsync(facts, cancellationToken),
            LostRateOverTime = await LostBusinessCharts.BuildLostRateOverTimeAsync(facts, cancellationToken),
            CancelledBy = await LostBusinessCharts.BuildCancelledByAsync(facts, cancellationToken)
        };
}
