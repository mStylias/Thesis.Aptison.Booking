namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>Every chart's data: an axis and the series lined up with it. Says nothing about how to draw it.</summary>
public class SeriesSetViewDto
{
    public required AnalyticsAxisViewDto Axis { get; init; }
    public required List<AnalyticsSeriesViewDto> Series { get; init; }
}
