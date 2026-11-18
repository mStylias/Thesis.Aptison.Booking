using Aptison.Booking.Application.Features.Customers.Common.Models;

namespace Aptison.Booking.Application.Features.Customers.Common.Responses;

/// <summary>
/// One customer as the drawer opens it: the row's members, the note and the block, and what the
/// caller may do here. Contact edits and merge apply to walk-ins only.
/// </summary>
public record CustomerResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public bool IsWalkIn { get; init; }
    public int VisitCount { get; init; }
    public DateOnly? FirstVisitDate { get; init; }
    public DateOnly? LastVisitDate { get; init; }
    public int? DaysSinceLastVisit { get; init; }
    public DateTimeOffset? NextAppointmentAt { get; init; }
    public int UpcomingCount { get; init; }
    public int NoShowCount { get; init; }
    public DateOnly? LastNoShowDate { get; init; }
    public int CancellationCount { get; init; }
    public Guid? PreferredStaffMemberId { get; init; }
    public string? PreferredStaffDisplayName { get; init; }
    public bool? PreferredStaffIsActive { get; init; }
    public DateOnly CustomerSince { get; init; }
    public decimal? TotalSpend { get; init; }
    public decimal? AverageSpend { get; init; }

    public CustomerNoteViewDto? Note { get; init; }

    /// <summary>The block in force at this shop. Its reason is internal and never reaches the customer.</summary>
    public CustomerBlockViewDto? Block { get; init; }

    public bool CanEditContactDetails { get; init; }
    public bool CanMerge { get; init; }
}
