using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>Where the money comes from.</summary>
public class RevenueTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto RevenueOverTime { get; init; }
    public required SeriesSetViewDto RevenuePerStaff { get; init; }
    public required SeriesSetViewDto RevenuePerService { get; init; }
}
