namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>
/// <see cref="Label"/> is set only for what has a name in the database; for fixed values the client
/// translates <see cref="Key"/>. A parent item carries the sum of its children.
/// </summary>
public class AnalyticsAxisItemViewDto
{
    public required string Key { get; init; }
    public string? Label { get; init; }
    public string? ParentKey { get; init; }

    /// <summary>Range axis bounds, inclusive. A null <see cref="To"/> is open-ended.</summary>
    public decimal? From { get; init; }

    public decimal? To { get; init; }

    /// <summary>Time axis: the bucket's first and last day, cut to the requested range.</summary>
    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }
}
