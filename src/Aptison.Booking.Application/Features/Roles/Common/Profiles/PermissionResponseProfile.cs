using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Domain.Entities.Authorization;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Roles.Common.Profiles;

/// <summary>
/// Entity → response map for the permission catalog. Automapping binds every member.
/// </summary>
/// <remarks>
/// No map for <c>GrantablePermissionResponse</c>: its <c>CanGrant</c> is a statement about the caller,
/// and the caller is not a column. The handler adds it to these rows in memory.
/// </remarks>
public class PermissionResponseProfile : MapperProfile
{
    public PermissionResponseProfile()
    {
        CreateMap<Permission, PermissionCatalogDto>();
    }
}
