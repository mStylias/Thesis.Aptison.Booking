using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Responses;

public class AnalyticsKpiResponse
{
    public required AnalyticsPeriodViewDto Period { get; init; }
    public required KpiViewDto Kpi { get; init; }
}
