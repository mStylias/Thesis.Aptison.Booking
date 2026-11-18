using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>Whether customers are happy.</summary>
public class QualityTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto RatingOverTime { get; init; }
    public required SeriesSetViewDto RatingDistribution { get; init; }
}
