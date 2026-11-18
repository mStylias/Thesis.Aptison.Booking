using Aptison.Booking.Application.Features.Analytics.Common.Enums;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>One value per axis item, in axis order. Null means no data, unlike 0.</summary>
public class AnalyticsSeriesViewDto
{
    public required string Key { get; init; }
    public AnalyticsUnit Unit { get; init; }
    public required List<decimal?> Values { get; init; }
}
