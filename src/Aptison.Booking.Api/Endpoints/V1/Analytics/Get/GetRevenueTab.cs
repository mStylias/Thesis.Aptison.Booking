using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>Revenue over time, per staff member and per service.</summary>
public class GetRevenueTab : AnalyticsTabEndpoint<GetRevenueTabRequest, RevenueTabResponse>
{
    protected override string Route => "/revenue";
}
