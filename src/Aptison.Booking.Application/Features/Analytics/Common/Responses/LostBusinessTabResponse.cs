using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>What the shop is missing.</summary>
public class LostBusinessTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto StatusMix { get; init; }
    public required SeriesSetViewDto LostRateOverTime { get; init; }
    public required SeriesSetViewDto CancelledBy { get; init; }
}
