using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

/// <summary>Each KPI is one measure over the range, the comparison range, and each bucket for its trend.</summary>
internal static class OverviewKpis
{
    /// <summary>Validation rejects an undefined value before this is reached.</summary>
    public static Task<KpiViewDto> BuildAsync(AnalyticsKpi kpi, AnalyticsFacts facts, CancellationToken cancellationToken) =>
        kpi switch
        {
            AnalyticsKpi.Revenue => BuildRevenueAsync(facts, cancellationToken),
            AnalyticsKpi.Appointments => BuildAppointmentsAsync(facts, cancellationToken),
            AnalyticsKpi.AverageTicket => BuildAverageTicketAsync(facts, cancellationToken),
            AnalyticsKpi.UniqueCustomers => BuildUniqueCustomersAsync(facts, cancellationToken),
            AnalyticsKpi.LostRate => BuildLostRateAsync(facts, cancellationToken),
            AnalyticsKpi.AverageRating => BuildAverageRatingAsync(facts, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kpi), kpi, null)
        };

    public static Task<KpiViewDto> BuildRevenueAsync(AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildFromAppointmentsAsync(facts, AnalyticsMeasures.SumRevenue, AnalyticsUnit.Money, cancellationToken);

    public static Task<KpiViewDto> BuildAppointmentsAsync(AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildFromAppointmentsAsync(facts, AnalyticsMeasures.CountBooked, AnalyticsUnit.Count, cancellationToken);

    public static Task<KpiViewDto> BuildAverageTicketAsync(AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildFromAppointmentsAsync(facts, AnalyticsMeasures.GetAverageTicket, AnalyticsUnit.Money, cancellationToken);

    public static Task<KpiViewDto> BuildLostRateAsync(AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildFromAppointmentsAsync(facts, AnalyticsMeasures.GetLostRate, AnalyticsUnit.Percent, cancellationToken);

    public static Task<KpiViewDto> BuildUniqueCustomersAsync(AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildKpiAsync(
            facts,
            facts.GetCustomerVisitFactsAsync,
            visit => visit.LocalDate,
            AnalyticsMeasures.CountUniqueCustomers,
            AnalyticsUnit.Count,
            cancellationToken);

    public static Task<KpiViewDto> BuildAverageRatingAsync(AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildKpiAsync(
            facts,
            facts.GetReviewDailyFactsAsync,
            review => review.LocalDate,
            AnalyticsMeasures.GetAverageRating,
            AnalyticsUnit.Rating,
            cancellationToken);

    private static Task<KpiViewDto> BuildFromAppointmentsAsync(
        AnalyticsFacts facts,
        Func<IReadOnlyCollection<AppointmentDailyFacts>, decimal?> measure,
        AnalyticsUnit unit,
        CancellationToken cancellationToken) =>
        BuildKpiAsync(facts, facts.GetAppointmentDailyFactsAsync, fact => fact.LocalDate, measure, unit, cancellationToken);

    private static async Task<KpiViewDto> BuildKpiAsync<T>(
        AnalyticsFacts facts,
        Func<AnalyticsReadScope, CancellationToken, Task<IReadOnlyList<T>>> readAsync,
        Func<T, DateOnly> getDay,
        Func<IReadOnlyCollection<T>, decimal?> measure,
        AnalyticsUnit unit,
        CancellationToken cancellationToken)
    {
        var scope = facts.Scope;
        var current = await readAsync(scope.CurrentRange, cancellationToken);
        var previous = await readAsync(scope.ComparisonRange, cancellationToken);
        var value = measure([.. current]);
        var previousValue = measure([.. previous]);

        return new KpiViewDto
        {
            Unit = unit,
            Value = value,
            PreviousValue = previousValue,
            Change = AnalyticsMeasures.GetChange(value, previousValue),
            Trend = new SeriesSetViewDto
            {
                Axis = SeriesSetBuilder.BuildTimeAxis(scope.Buckets, scope.Granularity),
                Series =
                [
                    SeriesSetBuilder.CreateSeries(
                        "value", unit, SeriesSetBuilder.AggregateByBucket(current, getDay, scope.Buckets, measure))
                ]
            }
        };
    }
}
