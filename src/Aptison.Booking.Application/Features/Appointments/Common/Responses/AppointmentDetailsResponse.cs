using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Customers.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>
/// One appointment as the management drawer opens it, and what every management write returns.
/// Times are shop-local <c>HH:mm</c>; the <c>...At</c> members are UTC instants.
/// </summary>
public record AppointmentDetailsResponse
{
    public Guid Id { get; init; }
    public Guid StaffMemberId { get; init; }
    public string StaffDisplayName { get; init; } = null!;
    public Guid CustomerId { get; init; }
    public string CustomerDisplayName { get; init; } = null!;
    public string? CustomerEmail { get; init; }
    public string? CustomerPhoneNumber { get; init; }

    /// <summary>False for a walk-in the shop created.</summary>
    public bool CustomerHasAccount { get; init; }

    /// <summary>The shop's own note on the customer, not this appointment's.</summary>
    public string? CustomerNote { get; init; }

    public Guid? CustomerPreferredStaffMemberId { get; init; }
    public string? CustomerPreferredStaffDisplayName { get; init; }
    public bool? CustomerPreferredStaffIsActive { get; init; }
    public int CustomerNoShowCount { get; init; }

    /// <summary>The customer's block at this shop. Its reason is internal and never reaches the customer.</summary>
    public CustomerBlockViewDto? CustomerBlock { get; init; }

    public DateOnly LocalDate { get; init; }
    public string LocalStartTime { get; init; } = null!;
    public string LocalEndTime { get; init; } = null!;

    /// <summary>Staff set the end, so the length is not the sum of the services'.</summary>
    public bool DurationOverridden { get; init; }

    public string Status { get; init; } = null!;

    /// <summary><c>online</c> or <c>staff</c>.</summary>
    public string Source { get; init; } = null!;

    public string? Notes { get; init; }
    public string? InternalNotes { get; init; }
    public decimal TotalPrice { get; init; }
    public string Currency { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
    public string CreatedByDisplayName { get; init; } = null!;

    /// <summary>Null until the first change.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    public string? ModifiedByDisplayName { get; init; }
    public string? CancellationReason { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public string? CancelledByDisplayName { get; init; }

    /// <summary>The customer cancelled it online, rather than the shop.</summary>
    public bool CancelledByCustomer { get; init; }
    public IReadOnlyList<AppointmentDetailsServiceViewDto> Services { get; init; } = [];
}
