using Aptison.Booking.Application.Features.Analytics.Common.Enums;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

public class KpiViewDto
{
    public AnalyticsUnit Unit { get; init; }
    public decimal? Value { get; init; }
    public decimal? PreviousValue { get; init; }

    /// <summary>(Value − PreviousValue) ÷ PreviousValue; null when either is null or the previous is 0.</summary>
    public decimal? Change { get; init; }

    public required SeriesSetViewDto Trend { get; init; }
}
