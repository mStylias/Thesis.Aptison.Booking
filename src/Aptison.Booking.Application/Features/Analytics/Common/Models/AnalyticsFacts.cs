using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Domain;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>
/// What a chart builder reads from. Each port call is made at most once per range, so a tab's charts
/// share their queries. Not thread-safe: the reads share the request's connection and run in turn.
/// </summary>
public sealed class AnalyticsFacts(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, AnalyticsScope scope)
{
    private readonly Dictionary<(Type Row, AnalyticsReadScope Range), object> _rowsByRange = [];

    public AnalyticsScope Scope { get; } = scope;

    /// <summary>For names and schedules: ordinary entity reads, ownership-filtered to the request's company.</summary>
    public IUnitOfWork<BookingContext> UnitOfWork { get; } = unitOfWork;

    public Task<IReadOnlyList<AppointmentDailyFacts>> GetAppointmentDailyFactsAsync(
        AnalyticsReadScope range, CancellationToken cancellationToken) =>
        GetOrReadAsync(range, analyticsReader.ReadAppointmentDailyFactsAsync, cancellationToken);

    public Task<IReadOnlyList<ServiceLineFacts>> GetServiceLineFactsAsync(
        AnalyticsReadScope range, CancellationToken cancellationToken) =>
        GetOrReadAsync(range, analyticsReader.ReadServiceLineFactsAsync, cancellationToken);

    public Task<IReadOnlyList<HourlyFacts>> GetHourlyFactsAsync(
        AnalyticsReadScope range, CancellationToken cancellationToken) =>
        GetOrReadAsync(range, analyticsReader.ReadHourlyFactsAsync, cancellationToken);

    public Task<IReadOnlyList<LeadTimeFacts>> GetLeadTimeFactsAsync(
        AnalyticsReadScope range, CancellationToken cancellationToken) =>
        GetOrReadAsync(range, analyticsReader.ReadLeadTimeFactsAsync, cancellationToken);

    public Task<IReadOnlyList<CustomerVisitFacts>> GetCustomerVisitFactsAsync(
        AnalyticsReadScope range, CancellationToken cancellationToken) =>
        GetOrReadAsync(range, analyticsReader.ReadCustomerVisitFactsAsync, cancellationToken);

    public Task<IReadOnlyList<ReviewDailyFacts>> GetReviewDailyFactsAsync(
        AnalyticsReadScope range, CancellationToken cancellationToken) =>
        GetOrReadAsync(range, analyticsReader.ReadReviewDailyFactsAsync, cancellationToken);

    private async Task<IReadOnlyList<T>> GetOrReadAsync<T>(
        AnalyticsReadScope range,
        Func<AnalyticsReadScope, CancellationToken, Task<IReadOnlyList<T>>> readAsync,
        CancellationToken cancellationToken)
    {
        if (_rowsByRange.TryGetValue((typeof(T), range), out var cached))
        {
            return (IReadOnlyList<T>)cached;
        }

        var rows = await readAsync(range, cancellationToken);
        _rowsByRange[(typeof(T), range)] = rows;

        return rows;
    }
}
