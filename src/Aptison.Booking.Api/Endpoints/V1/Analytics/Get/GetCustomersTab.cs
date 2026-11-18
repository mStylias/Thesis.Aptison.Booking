using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>New against returning customers, gaps between visits and top customers.</summary>
public class GetCustomersTab : AnalyticsTabEndpoint<GetCustomersTabRequest, CustomersTabResponse>
{
    protected override string Route => "/customers";
}
