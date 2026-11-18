using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>Status mix, lost rate over time and who cancels.</summary>
public class GetLostBusinessTab : AnalyticsTabEndpoint<GetLostBusinessTabRequest, LostBusinessTabResponse>
{
    protected override string Route => "/lost-business";
}
