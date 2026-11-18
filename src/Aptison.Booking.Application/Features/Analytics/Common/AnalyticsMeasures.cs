using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Analytics.Common;

/// <summary>
/// The single definition of each number the dashboard shows. A KPI and the chart showing the same
/// number over time both call one of these, so they cannot disagree.
/// </summary>
internal static class AnalyticsMeasures
{
    public static decimal? SumRevenue(IEnumerable<AppointmentDailyFacts> facts) =>
        facts.Where(fact => AnalyticsStatuses.IsRevenue(fact.Status)).Sum(fact => fact.Revenue);

    public static decimal? CountCompleted(IEnumerable<AppointmentDailyFacts> facts) =>
        facts.Where(fact => AnalyticsStatuses.IsRevenue(fact.Status)).Sum(fact => fact.Appointments);

    public static decimal? CountBooked(IEnumerable<AppointmentDailyFacts> facts) =>
        facts.Where(fact => AnalyticsStatuses.IsBooked(fact.Status)).Sum(fact => fact.Appointments);

    public static decimal? CountWithStatus(IEnumerable<AppointmentDailyFacts> facts, string status) =>
        facts.Where(fact => fact.Status == status).Sum(fact => fact.Appointments);

    public static decimal? GetAverageTicket(IReadOnlyCollection<AppointmentDailyFacts> facts) =>
        Divide(SumRevenue(facts), CountCompleted(facts), 2);

    /// <summary>Cancelled and no-show, as a percent of settled outcomes.</summary>
    public static decimal? GetLostRate(IReadOnlyCollection<AppointmentDailyFacts> facts) =>
        GetShareOfResolved(facts, status => status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow);

    public static decimal? GetCancellationRate(IReadOnlyCollection<AppointmentDailyFacts> facts) =>
        GetShareOfResolved(facts, status => status == AppointmentStatus.Cancelled);

    public static decimal? GetNoShowRate(IReadOnlyCollection<AppointmentDailyFacts> facts) =>
        GetShareOfResolved(facts, status => status == AppointmentStatus.NoShow);

    public static decimal? CountUniqueCustomers(IEnumerable<CustomerVisitFacts> visits) =>
        visits.Select(visit => visit.CustomerId).Distinct().Count();

    public static decimal? GetAverageRating(IReadOnlyCollection<ReviewDailyFacts> reviews) =>
        Divide(reviews.Sum(review => (decimal)review.Rating * review.Reviews), CountReviews(reviews), 2);

    public static decimal? CountReviews(IEnumerable<ReviewDailyFacts> reviews) =>
        reviews.Sum(review => review.Reviews);

    /// <summary>Null when either side is missing or the denominator is zero.</summary>
    public static decimal? Divide(decimal? numerator, decimal? denominator, int decimals) =>
        numerator is { } top && denominator is { } bottom && bottom != 0
            ? Math.Round(top / bottom, decimals, MidpointRounding.AwayFromZero)
            : null;

    public static decimal? GetPercent(decimal? part, decimal? whole) =>
        Divide(part * 100, whole, 2);

    public static decimal? GetChange(decimal? value, decimal? previous) =>
        Divide(value - previous, previous, 4);

    private static decimal? GetShareOfResolved(
        IReadOnlyCollection<AppointmentDailyFacts> facts, Func<string, bool> isPart)
    {
        var resolved = facts.Where(fact => AnalyticsStatuses.IsResolved(fact.Status)).ToList();

        return GetPercent(
            resolved.Where(fact => isPart(fact.Status)).Sum(fact => fact.Appointments),
            resolved.Sum(fact => fact.Appointments));
    }
}
