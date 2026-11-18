using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Authorization;

public class RolePermission : IEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
