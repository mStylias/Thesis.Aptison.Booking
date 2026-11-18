using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

public class AnalyticsChartResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto SeriesSet { get; init; }
}
