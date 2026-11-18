using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Staff;

public class StaffMember : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // On the employment rather than the user, so each shop can show its own photo. An absolute URL
    // is served as-is; any other key is a path in the public bucket.
    public string? AvatarStorageKey { get; set; }

    // One of StaffJobTitleCodes.
    public string? JobTitleCode { get; set; }

    // Separates who takes appointments from who merely works here (owners, receptionists).
    public bool IsBookable { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; }

    public ICollection<StaffWorkHour> WorkHours { get; set; } = [];
}
