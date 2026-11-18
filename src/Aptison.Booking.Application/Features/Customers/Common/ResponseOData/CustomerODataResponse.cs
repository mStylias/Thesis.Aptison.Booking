using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Customers.Common.ResponseOData;

/// <summary>
/// One row of the customers page: someone with an appointment at the shop. A visit is a completed
/// appointment; dates are shop-local, <c>...At</c> a UTC instant. The spend members are null for a
/// caller without <c>analytics:read</c>, never zero.
/// </summary>
public record CustomerODataResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;

    /// <summary>The person's own phone once they set one, until then the one the shop typed.</summary>
    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    /// <summary>False for an account holder, whose name, email and phone are their own.</summary>
    public bool IsWalkIn { get; init; }

    public int VisitCount { get; init; }
    public DateOnly? FirstVisitDate { get; init; }
    public DateOnly? LastVisitDate { get; init; }

    /// <summary>Counted in the shop's time zone. Null without a visit.</summary>
    public int? DaysSinceLastVisit { get; init; }

    public DateTimeOffset? NextAppointmentAt { get; init; }
    public int UpcomingCount { get; init; }
    public int NoShowCount { get; init; }
    public DateOnly? LastNoShowDate { get; init; }
    public int CancellationCount { get; init; }
    public Guid? PreferredStaffMemberId { get; init; }
    public string? PreferredStaffDisplayName { get; init; }
    public bool? PreferredStaffIsActive { get; init; }
    public bool HasNote { get; init; }
    public bool IsBlocked { get; init; }

    /// <summary>The date of the first appointment here, of any status.</summary>
    public DateOnly CustomerSince { get; init; }

    public decimal? TotalSpend { get; init; }
    public decimal? AverageSpend { get; init; }

    [JsonIgnore]
    public string NameNormalized { get; init; } = null!;
}
