using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Domain.Entities.Authorization;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Roles.Common.Profiles;

/// <summary>
/// Entity → response map. No <c>Role</c> → <c>RoleResponse</c> map: that shape carries permission
/// codes and a caller-dependent <c>IsEditable</c>, neither of which exists in SQL, so
/// <c>RoleReader</c> composes it from this projection and a second query.
/// </summary>
public class RoleResponseProfile : MapperProfile
{
    public RoleResponseProfile()
    {
        CreateMap<Role, RoleSummaryDto>();
    }
}
