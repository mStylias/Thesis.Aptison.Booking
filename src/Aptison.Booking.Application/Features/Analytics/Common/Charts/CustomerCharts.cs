using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Readers;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

/// <summary>Built from completed visits only.</summary>
internal static class CustomerCharts
{
    private static readonly AnalyticsBand[] GapBands =
    [
        new(1, 14), new(15, 28), new(29, 42), new(43, 56), new(57, 90), new(91, null)
    ];

    /// <summary>
    /// A customer is new in the bucket holding their first visit to the shop ever, and returning in
    /// any later one. Each customer counts once per bucket.
    /// </summary>
    public static async Task<SeriesSetViewDto> BuildNewVsReturningAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var scope = facts.Scope;
        var visits = await facts.GetCustomerVisitFactsAsync(scope.CurrentRange, cancellationToken);

        var newCustomers = SeriesSetBuilder.AggregateByBucket(
            visits,
            visit => visit.LocalDate,
            scope.Buckets,
            bucket => AnalyticsMeasures.CountUniqueCustomers(bucket.Where(visit =>
                AnalyticsCalendar.FindBucketIndex(scope.Buckets, visit.FirstVisitOn)
                == AnalyticsCalendar.FindBucketIndex(scope.Buckets, visit.LocalDate))));

        var allCustomers = SeriesSetBuilder.AggregateByBucket(
            visits, visit => visit.LocalDate, scope.Buckets, AnalyticsMeasures.CountUniqueCustomers);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildTimeAxis(scope.Buckets, scope.Granularity),
            Series =
            [
                SeriesSetBuilder.CreateSeries("new", AnalyticsUnit.Count, newCustomers),
                SeriesSetBuilder.CreateSeries(
                    "returning", AnalyticsUnit.Count, allCustomers.Zip(newCustomers, (all, first) => all - first))
            ]
        };
    }

    /// <summary>Days since the same customer's previous visit, for each visit that has one. Same-day visits count as one day.</summary>
    public static async Task<SeriesSetViewDto> BuildDaysBetweenVisitsAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var gaps = (await facts.GetCustomerVisitFactsAsync(facts.Scope.CurrentRange, cancellationToken))
            .Where(visit => visit.PreviousVisitOn is not null)
            .Select(visit => Math.Max(1, visit.LocalDate.DayNumber - visit.PreviousVisitOn!.Value.DayNumber))
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildRangeAxis(GapBands),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "visits", AnalyticsUnit.Count, GapBands.Select(band => (decimal?)gaps.Count(band.Contains)))
            ]
        };
    }

    /// <summary>By spend, then visits; <see cref="AnalyticsScope.Limit"/> customers.</summary>
    public static async Task<SeriesSetViewDto> BuildTopCustomersAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var visits = await facts.GetCustomerVisitFactsAsync(facts.Scope.CurrentRange, cancellationToken);

        var customers = visits
            .GroupBy(visit => visit.CustomerId)
            .Select(customer => new
            {
                CustomerId = customer.Key,
                Spend = customer.Sum(visit => visit.TotalPrice),
                Visits = customer.Count()
            })
            .OrderByDescending(customer => customer.Spend)
            .ThenByDescending(customer => customer.Visits)
            .ThenBy(customer => customer.CustomerId)
            .Take(facts.Scope.Limit)
            .ToList();

        var namesByIds = await AnalyticsLabelReader.ReadCustomerNamesAsync(
            facts, [.. customers.Select(customer => customer.CustomerId)], cancellationToken);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis(customers.Select(customer => new AnalyticsAxisItemViewDto
            {
                Key = SeriesSetBuilder.ToKey(customer.CustomerId),
                Label = namesByIds.GetValueOrDefault(customer.CustomerId)
            })),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "spend", AnalyticsUnit.Money, customers.Select(customer => (decimal?)customer.Spend)),
                SeriesSetBuilder.CreateSeries(
                    "visits", AnalyticsUnit.Count, customers.Select(customer => (decimal?)customer.Visits))
            ]
        };
    }
}
