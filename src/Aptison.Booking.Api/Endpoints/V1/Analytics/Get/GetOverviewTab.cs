using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>The six KPI tiles.</summary>
public class GetOverviewTab : AnalyticsTabEndpoint<GetOverviewTabRequest, OverviewTabResponse>
{
    protected override string Route => "/overview";
}
