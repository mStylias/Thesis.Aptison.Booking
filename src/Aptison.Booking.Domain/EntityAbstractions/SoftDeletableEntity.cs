using Synax.EntityFrameworkCore.Abstractions.SoftDelete;

namespace Aptison.Booking.Domain.EntityAbstractions;

public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeleteEntity<User, Guid?>
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
    public User? DeletedBy { get; set; }
}
