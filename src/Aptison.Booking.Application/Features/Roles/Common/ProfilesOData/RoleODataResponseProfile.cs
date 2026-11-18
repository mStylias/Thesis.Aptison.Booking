using Aptison.Booking.Application.Features.Roles.Common.ResponseOData;
using Aptison.Booking.Domain.Entities.Authorization;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Roles.Common.ProfilesOData;

/// <summary>
/// Entity → response map for the OData role grid; both computed members are expressions because
/// OData filters against the projection.
/// </summary>
/// <remarks>
/// A profile is built at startup and cannot be handed the request's company. It does not need to be:
/// under the ownership filter, <c>OwnerId != null</c> already means "this company's". That holds
/// only while the filter is on — a <c>BecomeAllOwners</c> scope around this projection would mark
/// another company's roles editable and attach their grant counts.
/// </remarks>
public class RoleODataResponseProfile : MapperProfile
{
    public RoleODataResponseProfile()
    {
        CreateMap<Role, RoleODataResponse>()
            // Same rule as RoleReader's in-memory line.
            .For(dto => dto.IsEditable, role => role.OwnerId != null && !role.IsSystem)
            // Not redundant: user_roles has an optional owner, so the filter admits global grants
            // and the count would otherwise include platform-wide super_admin holders.
            .For(dto => dto.NumberOfUsers, role => role.UserRoles.Count(grant => grant.OwnerId != null));
    }
}
