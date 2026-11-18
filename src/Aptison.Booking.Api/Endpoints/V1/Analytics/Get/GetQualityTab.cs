using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>Ratings over time and their distribution.</summary>
public class GetQualityTab : AnalyticsTabEndpoint<GetQualityTabRequest, QualityTabResponse>
{
    protected override string Route => "/quality";
}
