using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;

/// <summary>
/// One appointment in the company's book: a calendar event, or a row of the appointments grid.
/// Settable members rather than a positional record: EF can compose OData's <c>$filter</c> over an
/// object initializer, not over a constructor call. <c>StartsAt</c>, <c>EndsAt</c> and every
/// <c>...At</c> are UTC instants; the <c>Local...</c> members are shop-local, for the calendar.
/// </summary>
public record AppointmentODataResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public DateOnly LocalDate { get; init; }
    public string LocalStartTime { get; init; } = null!;
    public string LocalEndTime { get; init; } = null!;
    public int DurationMinutes { get; init; }
    public Guid StaffMemberId { get; init; }
    public string StaffDisplayName { get; init; } = null!;
    public Guid CustomerId { get; init; }
    public string CustomerDisplayName { get; init; } = null!;
    public string? CustomerEmail { get; init; }
    public string? CustomerPhoneNumber { get; init; }
    public bool CustomerHasNote { get; init; }
    public bool CustomerIsBlocked { get; init; }
    public int CustomerNoShowCount { get; init; }
    public string Status { get; init; } = null!;
    public string Source { get; init; } = null!;
    public IReadOnlyList<AppointmentServiceODataViewDto> Services { get; init; } = [];
    public decimal TotalPrice { get; init; }
    public string Currency { get; init; } = null!;
    public bool HasNotes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Null without a live review. An unpublished review still counts.</summary>
    public short? Rating { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }
    public string? CancelledByDisplayName { get; init; }

    /// <summary>The customer cancelled it online, rather than the shop.</summary>
    public bool CancelledByCustomer { get; init; }

    /// <summary>
    /// The canceller's full name, folded: the target of the "Cancelled by" <c>contains</c>. The
    /// display name is <c>First L.</c>, so a surname only matches here.
    /// </summary>
    [JsonIgnore]
    public string? CancelledByNameNormalized { get; init; }

    public string? CancellationReason { get; init; }

    [JsonIgnore]
    public string? CancellationReasonNormalized { get; init; }
}
