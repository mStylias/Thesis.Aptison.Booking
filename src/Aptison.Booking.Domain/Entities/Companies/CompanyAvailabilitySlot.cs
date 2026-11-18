using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Companies;

/// <summary>
/// One instant at which a staff member can start something, derived from business hours, work
/// hours, time off and the calendar. <see cref="MaxDurationMinutes"/> is what lets a single row
/// answer for every service: it fits when the service's duration plus buffers is no longer.
/// </summary>
public class CompanyAvailabilitySlot : IEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    // No StaffMember navigation: staff_members is ownership-filtered and this table is read with no
    // company scope, where the navigation would come back null on every row.
    public Guid StaffMemberId { get; set; }

    public DateTime StartsAt { get; set; }

    /// <summary>The day and minute in the company's own timezone — stored, because the filter is
    /// asked in local terms and no index can help an <c>AT TIME ZONE</c> on a column.</summary>
    public DateOnly LocalDay { get; set; }

    public short LocalMinute { get; set; }

    public short MaxDurationMinutes { get; set; }
}
