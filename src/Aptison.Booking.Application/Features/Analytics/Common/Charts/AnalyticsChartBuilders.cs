using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

/// <summary>The one place a chart id meets the code that builds it. A tab calls the same builders directly.</summary>
internal static class AnalyticsChartBuilders
{
    /// <summary>Validation rejects an undefined value before this is reached.</summary>
    public static Task<SeriesSetViewDto> BuildAsync(
        AnalyticsChart chart, AnalyticsFacts facts, CancellationToken cancellationToken) =>
        chart switch
        {
            AnalyticsChart.RevenueOverTime => RevenueCharts.BuildRevenueOverTimeAsync(facts, cancellationToken),
            AnalyticsChart.RevenuePerStaff => RevenueCharts.BuildRevenuePerStaffAsync(facts, cancellationToken),
            AnalyticsChart.RevenuePerService => ServiceCharts.BuildRevenuePerServiceAsync(facts, cancellationToken),
            AnalyticsChart.AppointmentsByStatus => DemandCharts.BuildAppointmentsByStatusAsync(facts, cancellationToken),
            AnalyticsChart.UpcomingLoad => DemandCharts.BuildUpcomingLoadAsync(facts, cancellationToken),
            AnalyticsChart.BusiestHours => DemandCharts.BuildBusiestHoursAsync(facts, cancellationToken),
            AnalyticsChart.BusyHeatmap => DemandCharts.BuildBusyHeatmapAsync(facts, cancellationToken),
            AnalyticsChart.BookingLeadTime => DemandCharts.BuildBookingLeadTimeAsync(facts, cancellationToken),
            AnalyticsChart.BookingSource => DemandCharts.BuildBookingSourceAsync(facts, cancellationToken),
            AnalyticsChart.AppointmentsPerStaff => TeamCharts.BuildAppointmentsPerStaffAsync(facts, cancellationToken),
            AnalyticsChart.StaffUtilization => TeamCharts.BuildStaffUtilizationAsync(facts, cancellationToken),
            AnalyticsChart.RatingPerStaff => TeamCharts.BuildRatingPerStaffAsync(facts, cancellationToken),
            AnalyticsChart.BookingsPerService => ServiceCharts.BuildBookingsPerServiceAsync(facts, cancellationToken),
            AnalyticsChart.NewVsReturning => CustomerCharts.BuildNewVsReturningAsync(facts, cancellationToken),
            AnalyticsChart.DaysBetweenVisits => CustomerCharts.BuildDaysBetweenVisitsAsync(facts, cancellationToken),
            AnalyticsChart.TopCustomers => CustomerCharts.BuildTopCustomersAsync(facts, cancellationToken),
            AnalyticsChart.StatusMix => LostBusinessCharts.BuildStatusMixAsync(facts, cancellationToken),
            AnalyticsChart.LostRateOverTime => LostBusinessCharts.BuildLostRateOverTimeAsync(facts, cancellationToken),
            AnalyticsChart.CancelledBy => LostBusinessCharts.BuildCancelledByAsync(facts, cancellationToken),
            AnalyticsChart.RatingOverTime => QualityCharts.BuildRatingOverTimeAsync(facts, cancellationToken),
            AnalyticsChart.RatingDistribution => QualityCharts.BuildRatingDistributionAsync(facts, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(chart), chart, null)
        };
}
