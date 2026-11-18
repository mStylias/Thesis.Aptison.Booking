using System.Globalization;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common;

/// <summary>Assembles <see cref="SeriesSetViewDto"/>s: axes, series and the per-bucket grouping every time chart needs.</summary>
internal static class SeriesSetBuilder
{
    public static AnalyticsAxisViewDto BuildTimeAxis(IReadOnlyList<AnalyticsBucket> buckets, AnalyticsGranularity granularity) =>
        new()
        {
            Kind = AnalyticsAxisKind.Time,
            Granularity = granularity,
            Items = [.. buckets.Select(bucket => new AnalyticsAxisItemViewDto
            {
                Key = bucket.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FromDate = bucket.From,
                ToDate = bucket.To
            })]
        };

    public static AnalyticsAxisViewDto BuildCategoryAxis(IEnumerable<AnalyticsAxisItemViewDto> items) =>
        new() { Kind = AnalyticsAxisKind.Category, Items = [.. items] };

    public static AnalyticsAxisViewDto BuildCategoryAxis(IEnumerable<string> keys) =>
        BuildCategoryAxis(keys.Select(key => new AnalyticsAxisItemViewDto { Key = key }));

    public static AnalyticsAxisViewDto BuildRangeAxis(IEnumerable<AnalyticsBand> bands) =>
        new()
        {
            Kind = AnalyticsAxisKind.Range,
            Items = [.. bands.Select(band => new AnalyticsAxisItemViewDto
            {
                Key = band.GetKey(),
                From = band.From,
                To = band.To
            })]
        };

    public static AnalyticsSeriesViewDto CreateSeries(string key, AnalyticsUnit unit, IEnumerable<decimal?> values) =>
        new() { Key = key, Unit = unit, Values = [.. values] };

    /// <summary>
    /// One value per bucket. An empty bucket is passed an empty list, so the measure decides whether
    /// it reads 0 (a sum) or null (an average).
    /// </summary>
    public static List<decimal?> AggregateByBucket<T>(
        IEnumerable<T> rows,
        Func<T, DateOnly> getDay,
        IReadOnlyList<AnalyticsBucket> buckets,
        Func<IReadOnlyCollection<T>, decimal?> aggregate)
    {
        var rowsByBucket = buckets.Select(_ => new List<T>()).ToArray();

        foreach (var row in rows)
        {
            var index = AnalyticsCalendar.FindBucketIndex(buckets, getDay(row));

            if (index >= 0)
            {
                rowsByBucket[index].Add(row);
            }
        }

        return [.. rowsByBucket.Select(bucketRows => aggregate(bucketRows))];
    }

    /// <summary>A comparison series lined up by position: padded with nulls, or cut, to the current axis.</summary>
    public static List<decimal?> AlignToCount(List<decimal?> values, int count) =>
        [.. values.Take(count), .. Enumerable.Repeat<decimal?>(null, Math.Max(0, count - values.Count))];

    public static string ToKey(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string ToKey(Guid value) => value.ToString("D");
}
