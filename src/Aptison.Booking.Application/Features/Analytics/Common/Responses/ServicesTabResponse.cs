using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>What sells.</summary>
public class ServicesTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto BookingsPerService { get; init; }
    public required SeriesSetViewDto RevenuePerService { get; init; }
}
