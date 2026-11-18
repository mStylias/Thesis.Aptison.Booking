using Aptison.Booking.Application.Features.UserResources.Commands;
using Aptison.Booking.Application.Features.UserResources.Common.Models;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.UserResources.Common.Profiles;

/// <summary>
/// Normalization lives in the map, so a member added to either side fails
/// <c>AssertConfigurationIsValid</c> at startup instead of silently never being patched.
/// </summary>
internal class UserResourcePatchProfile : MapperProfile
{
    public UserResourcePatchProfile()
    {
        CreateMap<UpdateUserResourceRequest, UserResourcePatch>()
            .For(d => d.Name, s => s.Name.IsSet
                ? PatchValue<string>.Of(s.Name.Value!.Trim())
                : PatchValue<string>.Unset)
            .For(d => d.Description, s => s.Description.IsSet
                ? PatchValue<string?>.Of(string.IsNullOrWhiteSpace(s.Description.Value)
                    ? null
                    : s.Description.Value!.Trim())
                : PatchValue<string?>.Unset);
    }
}
