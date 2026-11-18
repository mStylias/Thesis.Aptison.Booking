using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>A validated analytics request, with every default applied.</summary>
public sealed class AnalyticsScope
{
    public Guid CompanyId { get; init; }
    public Guid? StaffMemberId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public AnalyticsGranularity Granularity { get; init; }
    public AnalyticsComparison Comparison { get; init; }
    public DateOnly ComparisonFrom { get; init; }
    public DateOnly ComparisonTo { get; init; }
    public required IReadOnlyList<AnalyticsBucket> Buckets { get; init; }
    public required IReadOnlyList<AnalyticsBucket> ComparisonBuckets { get; init; }
    public required string Currency { get; init; }
    public required TimeZoneInfo TimeZone { get; init; }
    public Guid? LanguageId { get; init; }
    public int Limit { get; init; }
    public int Days { get; init; }

    /// <summary>The shop's local date when the request was validated.</summary>
    public DateOnly Today { get; init; }

    public AnalyticsReadScope CurrentRange => new(CompanyId, From, To, StaffMemberId);

    /// <summary>For the charts that compare staff members, where the staff filter does not apply.</summary>
    public AnalyticsReadScope CurrentRangeOfAllStaff => new(CompanyId, From, To, null);

    public AnalyticsReadScope ComparisonRange => new(CompanyId, ComparisonFrom, ComparisonTo, StaffMemberId);

    public AnalyticsReadScope UpcomingRange => new(CompanyId, Today, Today.AddDays(Days - 1), StaffMemberId);
}
