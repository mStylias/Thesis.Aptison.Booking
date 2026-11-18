using Synax.EntityFrameworkCore.Abstractions.Auditing;

namespace Aptison.Booking.Domain.EntityAbstractions;

/// <summary>
/// Base for entities carrying the standard audit trail. Every member is stamped by Synax's auditing
/// interceptor — application code never assigns them. <see cref="UpdatedAt"/> is null until the
/// first modification, which is why the column carries no <c>DEFAULT now()</c>.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditedEntity<User, Guid?>
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public User? ModifiedBy { get; set; }
}
