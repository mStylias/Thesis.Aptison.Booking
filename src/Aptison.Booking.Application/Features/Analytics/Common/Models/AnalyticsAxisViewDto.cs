using Aptison.Booking.Application.Features.Analytics.Common.Enums;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

public class AnalyticsAxisViewDto
{
    public AnalyticsAxisKind Kind { get; init; }

    /// <summary>Time axis only.</summary>
    public AnalyticsGranularity? Granularity { get; init; }

    public required List<AnalyticsAxisItemViewDto> Items { get; init; }
}
