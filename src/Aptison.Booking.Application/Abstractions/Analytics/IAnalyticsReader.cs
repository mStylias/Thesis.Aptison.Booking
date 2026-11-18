namespace Aptison.Booking.Application.Abstractions.Analytics;

/// <summary>
/// The dashboard's aggregates, grouped by day or smaller and never bucketed: Application does that.
/// Rows carry every status, so which ones count stays an Application rule. The implementation
/// bypasses the ownership filter and must narrow every query to <see cref="AnalyticsReadScope.CompanyId"/>.
/// </summary>
public interface IAnalyticsReader
{
    Task<IReadOnlyList<AppointmentDailyFacts>> ReadAppointmentDailyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceLineFacts>> ReadServiceLineFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<HourlyFacts>> ReadHourlyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadTimeFacts>> ReadLeadTimeFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken);

    /// <summary>Completed visits in the range only, with each customer's history at the shop.</summary>
    Task<IReadOnlyList<CustomerVisitFacts>> ReadCustomerVisitFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewDailyFacts>> ReadReviewDailyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken);
}
