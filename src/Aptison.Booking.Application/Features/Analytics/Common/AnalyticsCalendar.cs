using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common;

/// <summary>The date arithmetic behind every time axis: buckets, the default granularity and the comparison range.</summary>
internal static class AnalyticsCalendar
{
    public static AnalyticsGranularity GetDefaultGranularity(DateOnly from, DateOnly to) =>
        CountDays(from, to) switch
        {
            <= 31 => AnalyticsGranularity.Day,
            <= 184 => AnalyticsGranularity.Week,
            _ => AnalyticsGranularity.Month
        };

    public static int CountDays(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    public static (DateOnly From, DateOnly To) GetComparisonRange(
        DateOnly from, DateOnly to, AnalyticsComparison comparison)
    {
        if (comparison == AnalyticsComparison.PreviousYear)
        {
            // AddYears turns 29 February into 28 February.
            return (from.AddYears(-1), to.AddYears(-1));
        }

        var comparisonTo = from.AddDays(-1);

        return (comparisonTo.AddDays(1 - CountDays(from, to)), comparisonTo);
    }

    /// <summary>Weeks start on Monday. The first and last bucket are cut to the range.</summary>
    public static List<AnalyticsBucket> BuildBuckets(DateOnly from, DateOnly to, AnalyticsGranularity granularity)
    {
        var buckets = new List<AnalyticsBucket>();

        for (var start = from; start <= to;)
        {
            var end = GetBucketEnd(start, granularity);

            if (end > to)
            {
                end = to;
            }

            buckets.Add(new AnalyticsBucket(start, end));
            start = end.AddDays(1);
        }

        return buckets;
    }

    /// <summary>-1 when the day is outside every bucket.</summary>
    public static int FindBucketIndex(IReadOnlyList<AnalyticsBucket> buckets, DateOnly day)
    {
        var low = 0;
        var high = buckets.Count - 1;

        while (low <= high)
        {
            var middle = (low + high) / 2;

            if (day < buckets[middle].From)
            {
                high = middle - 1;
            }
            else if (day > buckets[middle].To)
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return -1;
    }

    private static DateOnly GetBucketEnd(DateOnly start, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Week => start.AddDays(((int)DayOfWeek.Sunday - (int)start.DayOfWeek + 7) % 7),
        AnalyticsGranularity.Month => new DateOnly(start.Year, start.Month, 1).AddMonths(1).AddDays(-1),
        _ => start
    };
}
