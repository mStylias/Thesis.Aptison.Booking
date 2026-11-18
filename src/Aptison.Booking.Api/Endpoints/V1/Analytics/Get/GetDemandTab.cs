using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>Bookings by status, upcoming load, busy hours, lead time and source.</summary>
public class GetDemandTab : AnalyticsTabEndpoint<GetDemandTabRequest, DemandTabResponse>
{
    protected override string Route => "/demand";
}
