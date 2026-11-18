using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>How the shop is doing.</summary>
public class OverviewTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required KpiViewDto Revenue { get; init; }
    public required KpiViewDto Appointments { get; init; }
    public required KpiViewDto AverageTicket { get; init; }
    public required KpiViewDto UniqueCustomers { get; init; }
    public required KpiViewDto LostRate { get; init; }
    public required KpiViewDto AverageRating { get; init; }
}
