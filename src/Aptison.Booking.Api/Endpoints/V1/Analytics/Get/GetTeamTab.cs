using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>Each staff member's appointments, utilization and rating.</summary>
public class GetTeamTab : AnalyticsTabEndpoint<GetTeamTabRequest, TeamTabResponse>
{
    protected override string Route => "/team";
}
