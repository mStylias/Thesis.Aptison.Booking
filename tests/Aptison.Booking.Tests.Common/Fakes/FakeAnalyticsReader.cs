using Aptison.Booking.Application.Abstractions.Analytics;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Serves the rows a test puts in. Dated rows are narrowed to the asked range and staff member, as
/// the SQL does; undated ones come back whole. Records every range it was asked for.
/// </summary>
public sealed class FakeAnalyticsReader : IAnalyticsReader
{
    private readonly List<(string Method, AnalyticsReadScope Scope)> _reads = [];

    public List<AppointmentDailyFacts> AppointmentDailyFacts { get; } = [];
    public List<ServiceLineFacts> ServiceLineFacts { get; } = [];
    public List<HourlyFacts> HourlyFacts { get; } = [];
    public List<LeadTimeFacts> LeadTimeFacts { get; } = [];
    public List<CustomerVisitFacts> CustomerVisitFacts { get; } = [];
    public List<ReviewDailyFacts> ReviewDailyFacts { get; } = [];

    public IReadOnlyList<(string Method, AnalyticsReadScope Scope)> Reads => _reads;

    public Task<IReadOnlyList<AppointmentDailyFacts>> ReadAppointmentDailyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        Read(nameof(ReadAppointmentDailyFactsAsync), scope, AppointmentDailyFacts
            .Where(fact => IsInRange(scope, fact.LocalDate) && IsForStaff(scope, fact.StaffMemberId)));

    public Task<IReadOnlyList<ServiceLineFacts>> ReadServiceLineFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        Read(nameof(ReadServiceLineFactsAsync), scope, ServiceLineFacts);

    public Task<IReadOnlyList<HourlyFacts>> ReadHourlyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        Read(nameof(ReadHourlyFactsAsync), scope, HourlyFacts);

    public Task<IReadOnlyList<LeadTimeFacts>> ReadLeadTimeFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        Read(nameof(ReadLeadTimeFactsAsync), scope, LeadTimeFacts);

    public Task<IReadOnlyList<CustomerVisitFacts>> ReadCustomerVisitFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        Read(nameof(ReadCustomerVisitFactsAsync), scope, CustomerVisitFacts
            .Where(visit => IsInRange(scope, visit.LocalDate)));

    public Task<IReadOnlyList<ReviewDailyFacts>> ReadReviewDailyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        Read(nameof(ReadReviewDailyFactsAsync), scope, ReviewDailyFacts
            .Where(review => IsInRange(scope, review.LocalDate) && IsForStaff(scope, review.StaffMemberId)));

    private Task<IReadOnlyList<T>> Read<T>(string method, AnalyticsReadScope scope, IEnumerable<T> rows)
    {
        _reads.Add((method, scope));

        return Task.FromResult<IReadOnlyList<T>>([.. rows]);
    }

    private static bool IsInRange(AnalyticsReadScope scope, DateOnly day) => day >= scope.From && day <= scope.To;

    private static bool IsForStaff(AnalyticsReadScope scope, Guid staffMemberId) =>
        scope.StaffMemberId is null || scope.StaffMemberId == staffMemberId;
}
