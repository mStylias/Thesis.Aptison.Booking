using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Readers;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

internal static class RevenueCharts
{
    /// <summary>The comparison range's revenue lines up by position as <c>previous</c>.</summary>
    public static async Task<SeriesSetViewDto> BuildRevenueOverTimeAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var scope = facts.Scope;
        var current = await facts.GetAppointmentDailyFactsAsync(scope.CurrentRange, cancellationToken);
        var previous = await facts.GetAppointmentDailyFactsAsync(scope.ComparisonRange, cancellationToken);

        var previousRevenue = SeriesSetBuilder.AggregateByBucket(
            previous, fact => fact.LocalDate, scope.ComparisonBuckets, AnalyticsMeasures.SumRevenue);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildTimeAxis(scope.Buckets, scope.Granularity),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "revenue",
                    AnalyticsUnit.Money,
                    SeriesSetBuilder.AggregateByBucket(
                        current, fact => fact.LocalDate, scope.Buckets, AnalyticsMeasures.SumRevenue)),
                SeriesSetBuilder.CreateSeries(
                    "previous",
                    AnalyticsUnit.Money,
                    SeriesSetBuilder.AlignToCount(previousRevenue, scope.Buckets.Count))
            ]
        };
    }

    public static async Task<SeriesSetViewDto> BuildRevenuePerStaffAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var current = await facts.GetAppointmentDailyFactsAsync(facts.Scope.CurrentRange, cancellationToken);

        var revenueByStaff = current
            .Where(fact => AnalyticsStatuses.IsRevenue(fact.Status))
            .GroupBy(fact => fact.StaffMemberId)
            .Select(staff => new
            {
                StaffMemberId = staff.Key,
                Revenue = AnalyticsMeasures.SumRevenue(staff),
                Appointments = AnalyticsMeasures.CountCompleted(staff)
            })
            .OrderByDescending(staff => staff.Revenue)
            .ThenBy(staff => staff.StaffMemberId)
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = await AnalyticsLabelReader.ReadStaffAxisAsync(
                facts, [.. revenueByStaff.Select(staff => staff.StaffMemberId)], cancellationToken),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "revenue", AnalyticsUnit.Money, revenueByStaff.Select(staff => staff.Revenue)),
                SeriesSetBuilder.CreateSeries(
                    "completedAppointments", AnalyticsUnit.Count, revenueByStaff.Select(staff => staff.Appointments))
            ]
        };
    }
}
