using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>Bookings and revenue per service.</summary>
public class GetServicesTab : AnalyticsTabEndpoint<GetServicesTabRequest, ServicesTabResponse>
{
    protected override string Route => "/services";
}
