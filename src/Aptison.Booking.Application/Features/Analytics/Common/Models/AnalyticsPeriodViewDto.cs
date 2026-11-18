using Aptison.Booking.Application.Features.Analytics.Common.Enums;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>What the numbers cover, after defaults: the granularity actually used and the comparison range.</summary>
public class AnalyticsPeriodViewDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public AnalyticsGranularity Granularity { get; init; }
    public AnalyticsComparison Comparison { get; init; }
    public DateOnly ComparisonFrom { get; init; }
    public DateOnly ComparisonTo { get; init; }
    public required string Currency { get; init; }
}
