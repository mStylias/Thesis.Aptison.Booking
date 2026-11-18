using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>When the shop is busy.</summary>
public class DemandTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto AppointmentsByStatus { get; init; }
    public required SeriesSetViewDto UpcomingLoad { get; init; }
    public required SeriesSetViewDto BusiestHours { get; init; }
    public required SeriesSetViewDto BusyHeatmap { get; init; }
    public required SeriesSetViewDto BookingLeadTime { get; init; }
    public required SeriesSetViewDto BookingSource { get; init; }
}
