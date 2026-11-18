using Aptison.Booking.Application.Features.Analytics.Common;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.UnitTests.Analytics.Common;

public class AnalyticsCalendarTests
{
    [Test]
    public async Task BuildBuckets_WhenWeeklyAndTheRangeStartsMidWeek_ShouldCutTheFirstAndLastWeek()
    {
        // Arrange
        var from = new DateOnly(2026, 8, 1); // Saturday
        var to = new DateOnly(2026, 8, 12); // Wednesday

        // Act
        var buckets = AnalyticsCalendar.BuildBuckets(from, to, AnalyticsGranularity.Week);

        // Assert
        await Assert.That(buckets).IsEquivalentTo(
            [
                new AnalyticsBucket(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2)),
                new AnalyticsBucket(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9)),
                new AnalyticsBucket(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12))
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task BuildBuckets_WhenMonthly_ShouldEndEachBucketOnTheLastDayOfItsMonth()
    {
        // Arrange
        var from = new DateOnly(2028, 1, 15);
        var to = new DateOnly(2028, 3, 10);

        // Act
        var buckets = AnalyticsCalendar.BuildBuckets(from, to, AnalyticsGranularity.Month);

        // Assert
        await Assert.That(buckets).IsEquivalentTo(
            [
                new AnalyticsBucket(new DateOnly(2028, 1, 15), new DateOnly(2028, 1, 31)),
                new AnalyticsBucket(new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29)),
                new AnalyticsBucket(new DateOnly(2028, 3, 1), new DateOnly(2028, 3, 10))
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetComparisonRange_WhenPreviousPeriod_ShouldEndTheDayBeforeWithTheSameLength()
    {
        // Arrange
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 30);

        // Act
        var (comparisonFrom, comparisonTo) = AnalyticsCalendar.GetComparisonRange(
            from, to, AnalyticsComparison.PreviousPeriod);

        // Assert
        await Assert.That(comparisonFrom).IsEqualTo(new DateOnly(2026, 8, 2));
        await Assert.That(comparisonTo).IsEqualTo(new DateOnly(2026, 8, 31));
    }

    [Test]
    public async Task GetComparisonRange_WhenPreviousYearFromALeapDay_ShouldLandOnTheTwentyEighth()
    {
        // Arrange
        var from = new DateOnly(2028, 2, 1);
        var to = new DateOnly(2028, 2, 29);

        // Act
        var (comparisonFrom, comparisonTo) = AnalyticsCalendar.GetComparisonRange(
            from, to, AnalyticsComparison.PreviousYear);

        // Assert
        await Assert.That(comparisonFrom).IsEqualTo(new DateOnly(2027, 2, 1));
        await Assert.That(comparisonTo).IsEqualTo(new DateOnly(2027, 2, 28));
    }

    [Test]
    [Arguments(31, AnalyticsGranularity.Day)]
    [Arguments(32, AnalyticsGranularity.Week)]
    [Arguments(184, AnalyticsGranularity.Week)]
    [Arguments(185, AnalyticsGranularity.Month)]
    public async Task GetDefaultGranularity_WhenTheRangeHasThisManyDays_ShouldPickTheExpectedGranularity(
        int days, AnalyticsGranularity expected)
    {
        // Arrange
        var from = new DateOnly(2026, 1, 1);

        // Act
        var granularity = AnalyticsCalendar.GetDefaultGranularity(from, from.AddDays(days - 1));

        // Assert
        await Assert.That(granularity).IsEqualTo(expected);
    }

    [Test]
    public async Task FindBucketIndex_WhenTheDayIsOutsideEveryBucket_ShouldReturnMinusOne()
    {
        // Arrange
        var buckets = AnalyticsCalendar.BuildBuckets(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), AnalyticsGranularity.Week);

        // Act
        var before = AnalyticsCalendar.FindBucketIndex(buckets, new DateOnly(2026, 8, 31));
        var inside = AnalyticsCalendar.FindBucketIndex(buckets, new DateOnly(2026, 9, 9));
        var after = AnalyticsCalendar.FindBucketIndex(buckets, new DateOnly(2026, 10, 1));

        // Assert
        await Assert.That(before).IsEqualTo(-1);
        await Assert.That(inside).IsEqualTo(1);
        await Assert.That(after).IsEqualTo(-1);
    }
}
