using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

internal static class LostBusinessCharts
{
    public const string CancelledByCustomerKey = "customer";
    public const string CancelledByShopKey = "shop";

    public static async Task<SeriesSetViewDto> BuildStatusMixAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var current = await facts.GetAppointmentDailyFactsAsync(facts.Scope.CurrentRange, cancellationToken);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis(AnalyticsStatuses.Ordered),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "appointments",
                    AnalyticsUnit.Count,
                    AnalyticsStatuses.Ordered.Select(status => AnalyticsMeasures.CountWithStatus(current, status)))
            ]
        };
    }

    public static async Task<SeriesSetViewDto> BuildLostRateOverTimeAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var scope = facts.Scope;
        var current = await facts.GetAppointmentDailyFactsAsync(scope.CurrentRange, cancellationToken);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildTimeAxis(scope.Buckets, scope.Granularity),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "cancellationRate",
                    AnalyticsUnit.Percent,
                    SeriesSetBuilder.AggregateByBucket(
                        current, fact => fact.LocalDate, scope.Buckets, AnalyticsMeasures.GetCancellationRate)),
                SeriesSetBuilder.CreateSeries(
                    "noShowRate",
                    AnalyticsUnit.Percent,
                    SeriesSetBuilder.AggregateByBucket(
                        current, fact => fact.LocalDate, scope.Buckets, AnalyticsMeasures.GetNoShowRate))
            ]
        };
    }

    /// <summary>The customer when they cancelled their own booking, the shop otherwise.</summary>
    public static async Task<SeriesSetViewDto> BuildCancelledByAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var cancelled = (await facts.GetAppointmentDailyFactsAsync(facts.Scope.CurrentRange, cancellationToken))
            .Where(fact => fact.Status == AppointmentStatus.Cancelled)
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis([CancelledByCustomerKey, CancelledByShopKey]),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "appointments",
                    AnalyticsUnit.Count,
                    [
                        cancelled.Where(fact => fact.CancelledByCustomer).Sum(fact => fact.Appointments),
                        cancelled.Where(fact => !fact.CancelledByCustomer).Sum(fact => fact.Appointments)
                    ])
            ]
        };
    }
}
