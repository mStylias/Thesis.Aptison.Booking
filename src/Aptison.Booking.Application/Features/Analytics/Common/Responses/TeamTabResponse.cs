using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>How each staff member is doing. Ignores the staff filter.</summary>
public class TeamTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto AppointmentsPerStaff { get; init; }
    public required SeriesSetViewDto StaffUtilization { get; init; }
    public required SeriesSetViewDto RatingPerStaff { get; init; }
}
