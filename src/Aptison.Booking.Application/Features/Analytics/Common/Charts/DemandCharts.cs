using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Readers;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

internal static class DemandCharts
{
    private static readonly AnalyticsBand[] LeadTimeBands =
    [
        new(0, 0), new(1, 1), new(2, 3), new(4, 7), new(8, 14), new(15, 30), new(31, null)
    ];

    private static readonly string[] Sources = [AppointmentSource.Online, AppointmentSource.Staff];

    public static async Task<SeriesSetViewDto> BuildAppointmentsByStatusAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var scope = facts.Scope;
        var current = await facts.GetAppointmentDailyFactsAsync(scope.CurrentRange, cancellationToken);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildTimeAxis(scope.Buckets, scope.Granularity),
            Series =
            [
                .. AnalyticsStatuses.Ordered.Select(status => SeriesSetBuilder.CreateSeries(
                    status,
                    AnalyticsUnit.Count,
                    SeriesSetBuilder.AggregateByBucket(
                        current,
                        fact => fact.LocalDate,
                        scope.Buckets,
                        bucket => AnalyticsMeasures.CountWithStatus(bucket, status))))
            ]
        };
    }

    /// <summary>Pending and confirmed bookings by day, from today. Ignores the requested range.</summary>
    public static async Task<SeriesSetViewDto> BuildUpcomingLoadAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var range = facts.Scope.UpcomingRange;
        var buckets = AnalyticsCalendar.BuildBuckets(range.From, range.To, AnalyticsGranularity.Day);

        var upcoming = (await facts.GetAppointmentDailyFactsAsync(range, cancellationToken))
            .Where(fact => AnalyticsStatuses.IsUpcoming(fact.Status))
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildTimeAxis(buckets, AnalyticsGranularity.Day),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "appointments",
                    AnalyticsUnit.Count,
                    SeriesSetBuilder.AggregateByBucket(
                        upcoming, fact => fact.LocalDate, buckets, bucket => bucket.Sum(fact => fact.Appointments))),
                SeriesSetBuilder.CreateSeries(
                    "bookedMinutes",
                    AnalyticsUnit.Minutes,
                    SeriesSetBuilder.AggregateByBucket(
                        upcoming, fact => fact.LocalDate, buckets, bucket => bucket.Sum(fact => fact.Minutes)))
            ]
        };
    }

    public static async Task<SeriesSetViewDto> BuildBusiestHoursAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var booked = await ReadBookedHourlyFactsAsync(facts, cancellationToken);
        var hours = await GetHoursAsync(facts, booked, cancellationToken);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis(hours.Select(SeriesSetBuilder.ToKey)),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "appointments",
                    AnalyticsUnit.Count,
                    hours.Select(hour => (decimal?)booked.Where(fact => fact.Hour == hour).Sum(fact => fact.Appointments)))
            ]
        };
    }

    /// <summary>One series per ISO weekday, keyed 1 (Monday) to 7, over the hours of the day.</summary>
    public static async Task<SeriesSetViewDto> BuildBusyHeatmapAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var booked = await ReadBookedHourlyFactsAsync(facts, cancellationToken);
        var hours = await GetHoursAsync(facts, booked, cancellationToken);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis(hours.Select(SeriesSetBuilder.ToKey)),
            Series =
            [
                .. Enumerable.Range(1, 7).Select(weekday => SeriesSetBuilder.CreateSeries(
                    SeriesSetBuilder.ToKey(weekday),
                    AnalyticsUnit.Count,
                    hours.Select(hour => (decimal?)booked
                        .Where(fact => fact.IsoWeekday == weekday && fact.Hour == hour)
                        .Sum(fact => fact.Appointments))))
            ]
        };
    }

    /// <summary>A booking entered after its own date counts as booked the same day.</summary>
    public static async Task<SeriesSetViewDto> BuildBookingLeadTimeAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var booked = (await facts.GetLeadTimeFactsAsync(facts.Scope.CurrentRange, cancellationToken))
            .Where(fact => AnalyticsStatuses.IsBooked(fact.Status))
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildRangeAxis(LeadTimeBands),
            Series =
            [
                .. Sources.Select(source => SeriesSetBuilder.CreateSeries(
                    source,
                    AnalyticsUnit.Count,
                    LeadTimeBands.Select(band => (decimal?)booked
                        .Where(fact => fact.Source == source && band.Contains(Math.Max(0, fact.LeadDays)))
                        .Sum(fact => fact.Appointments))))
            ]
        };
    }

    public static async Task<SeriesSetViewDto> BuildBookingSourceAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var booked = (await facts.GetAppointmentDailyFactsAsync(facts.Scope.CurrentRange, cancellationToken))
            .Where(fact => AnalyticsStatuses.IsBooked(fact.Status))
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis(Sources),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "appointments",
                    AnalyticsUnit.Count,
                    Sources.Select(source => (decimal?)booked.Where(fact => fact.Source == source).Sum(fact => fact.Appointments)))
            ]
        };
    }

    private static async Task<List<HourlyFacts>> ReadBookedHourlyFactsAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken) =>
        [.. (await facts.GetHourlyFactsAsync(facts.Scope.CurrentRange, cancellationToken))
            .Where(fact => AnalyticsStatuses.IsBooked(fact.Status))];

    /// <summary>Every hour from the earliest to the latest that the shop is open or had a booking, with no gaps.</summary>
    private static async Task<List<int>> GetHoursAsync(
        AnalyticsFacts facts, IReadOnlyList<HourlyFacts> booked, CancellationToken cancellationToken)
    {
        var hours = booked.Where(fact => fact.Appointments > 0).Select(fact => fact.Hour).ToList();

        if (await AnalyticsScheduleReader.ReadOpenHoursAsync(facts, cancellationToken) is { } openHours)
        {
            hours.Add(openHours.First);
            hours.Add(openHours.Last);
        }

        return hours.Count == 0 ? [] : [.. Enumerable.Range(hours.Min(), hours.Max() - hours.Min() + 1)];
    }
}
