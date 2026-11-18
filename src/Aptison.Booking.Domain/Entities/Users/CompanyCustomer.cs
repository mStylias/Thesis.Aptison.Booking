using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Users;

/// <summary>What one shop keeps about one customer. A row exists only once there is something to keep.</summary>
public class CompanyCustomer : AuditableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? Note { get; set; }
    public DateTime? NoteUpdatedAt { get; set; }
    public Guid? NoteUpdatedById { get; set; }
    public User? NoteUpdatedBy { get; set; }

    public Guid? PreferredStaffMemberId { get; set; }
    public StaffMember? PreferredStaffMember { get; set; }

    /// <summary>The phone the shop typed for a walk-in who has since signed up. The person never sees it.</summary>
    public string? KeptPhoneNumber { get; set; }
}
