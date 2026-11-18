using Aptison.Booking.Application.Features.Analytics.Common.Enums;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>The parameters every analytics query shares. A chart that has no use for one ignores it.</summary>
public abstract class AnalyticsRequest
{
    /// <summary>The request's resolved company, never bound from a caller-supplied value.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Shop-local, inclusive.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Shop-local, inclusive.</summary>
    public DateOnly? To { get; set; }

    /// <summary>Picked from the range length when null.</summary>
    public AnalyticsGranularity? Granularity { get; set; }

    /// <summary><see cref="AnalyticsComparison.PreviousPeriod"/> when null.</summary>
    public AnalyticsComparison? CompareTo { get; set; }

    /// <summary>Narrows everything but the staff comparisons to one staff member.</summary>
    public Guid? StaffMemberId { get; set; }

    /// <summary>The language of service and category names. Falls back to whichever each has.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>Top customers only.</summary>
    public int? Limit { get; set; }

    /// <summary>Upcoming load only: days from today, today included.</summary>
    public int? Days { get; set; }
}
