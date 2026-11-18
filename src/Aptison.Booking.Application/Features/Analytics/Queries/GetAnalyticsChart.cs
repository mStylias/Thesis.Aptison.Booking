using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common;
using Aptison.Booking.Application.Features.Analytics.Common.Charts;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Analytics.Queries;

/// <summary>One chart on its own, the same data its tab carries under the property of the same name.</summary>
public class GetAnalyticsChartRequest : AnalyticsRequest, IQuery<Result<AnalyticsChartResponse, HttpError>>
{
    public AnalyticsChart Chart { get; set; }
}

public class GetAnalyticsChartHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetAnalyticsChartRequest, AnalyticsChartResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    public override async Task<Result<AnalyticsScope, HttpError>> ValidateAsync(
        GetAnalyticsChartRequest request, CancellationToken cancellationToken) =>
        Enum.IsDefined(request.Chart)
            ? await base.ValidateAsync(request, cancellationToken)
            : HttpError.NotFound("The chart was not found.", errorCode: ErrorCodes.Analytics.UnknownChart);

    protected override async Task<AnalyticsChartResponse> BuildAsync(
        GetAnalyticsChartRequest request,
        AnalyticsFacts facts,
        AnalyticsPeriodViewDto period,
        CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            SeriesSet = await AnalyticsChartBuilders.BuildAsync(request.Chart, facts, cancellationToken)
        };
}
