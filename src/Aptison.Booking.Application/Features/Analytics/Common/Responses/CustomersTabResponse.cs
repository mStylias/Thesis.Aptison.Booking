using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

/// <summary>Whether people come back.</summary>
public class CustomersTabResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required SeriesSetViewDto NewVsReturning { get; init; }
    public required SeriesSetViewDto DaysBetweenVisits { get; init; }
    public required SeriesSetViewDto TopCustomers { get; init; }
}
