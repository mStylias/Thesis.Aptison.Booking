using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

/// <summary>Reviews are dated by the appointment they rate, not by when they were written.</summary>
internal static class QualityCharts
{
    public static async Task<SeriesSetViewDto> BuildRatingOverTimeAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var scope = facts.Scope;
        var reviews = await facts.GetReviewDailyFactsAsync(scope.CurrentRange, cancellationToken);

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildTimeAxis(scope.Buckets, scope.Granularity),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "averageRating",
                    AnalyticsUnit.Rating,
                    SeriesSetBuilder.AggregateByBucket(
                        reviews, review => review.LocalDate, scope.Buckets, AnalyticsMeasures.GetAverageRating)),
                SeriesSetBuilder.CreateSeries(
                    "reviews",
                    AnalyticsUnit.Count,
                    SeriesSetBuilder.AggregateByBucket(
                        reviews, review => review.LocalDate, scope.Buckets, AnalyticsMeasures.CountReviews))
            ]
        };
    }

    public static async Task<SeriesSetViewDto> BuildRatingDistributionAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var reviews = await facts.GetReviewDailyFactsAsync(facts.Scope.CurrentRange, cancellationToken);
        var ratings = Enumerable.Range(1, 5).ToList();

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis(ratings.Select(SeriesSetBuilder.ToKey)),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "reviews",
                    AnalyticsUnit.Count,
                    ratings.Select(rating => AnalyticsMeasures.CountReviews(reviews.Where(review => review.Rating == rating))))
            ]
        };
    }
}
