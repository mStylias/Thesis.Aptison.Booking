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

/// <summary>One KPI on its own, the same data the overview carries under the property of the same name.</summary>
public class GetAnalyticsKpiRequest : AnalyticsRequest, IQuery<Result<AnalyticsKpiResponse, HttpError>>
{
    public AnalyticsKpi Kpi { get; set; }
}

public class GetAnalyticsKpiHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetAnalyticsKpiRequest, AnalyticsKpiResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    public override async Task<Result<AnalyticsScope, HttpError>> ValidateAsync(
        GetAnalyticsKpiRequest request, CancellationToken cancellationToken) =>
        Enum.IsDefined(request.Kpi)
            ? await base.ValidateAsync(request, cancellationToken)
            : HttpError.NotFound("The KPI was not found.", errorCode: ErrorCodes.Analytics.UnknownKpi);

    protected override async Task<AnalyticsKpiResponse> BuildAsync(
        GetAnalyticsKpiRequest request,
        AnalyticsFacts facts,
        AnalyticsPeriodViewDto period,
        CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            Kpi = await OverviewKpis.BuildAsync(request.Kpi, facts, cancellationToken)
        };
}
