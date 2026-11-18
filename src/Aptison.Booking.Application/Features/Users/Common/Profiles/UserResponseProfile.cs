using Aptison.Booking.Application.Features.Users.Common.Models;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Users.Common.Profiles;

/// <summary>
/// Entity → response maps. <c>Language</c> needs an explicit <c>For</c> because the navigation is
/// named <c>PreferredLanguageEntity</c>; everything else automaps.
/// </summary>
public class UserResponseProfile : MapperProfile
{
    public UserResponseProfile()
    {
        CreateMap<User, UserResponse>()
            .For(d => d.Language, s => s.PreferredLanguageEntity);

        CreateMap<User, UserSummaryViewDto>();
    }
}
