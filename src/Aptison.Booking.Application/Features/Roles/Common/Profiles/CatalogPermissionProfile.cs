using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Domain.Entities.Authorization;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Roles.Common.Profiles;

/// <summary>
/// The two ways a grant decision reaches the catalog: directly from <c>permissions</c> when a role
/// is being written, and through <c>role_permissions</c> when one is being granted. The second
/// navigation is null-safe — EF translates it as an inner join behind a foreign key.
/// </summary>
public class CatalogPermissionProfile : MapperProfile
{
    public CatalogPermissionProfile()
    {
        CreateMap<Permission, CatalogPermission>();

        CreateMap<RolePermission, CatalogPermission>()
            .For(d => d.Id, s => s.PermissionId)
            .For(d => d.Code, s => s.Permission.Code)
            .For(d => d.IsTenantGrantable, s => s.Permission.IsTenantGrantable);
    }
}
