using Aptison.Booking.Application.Features.Users.Common.Models;
using Aptison.Booking.Domain.Entities.Reference;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Users.Common.Profiles;

/// <summary>
/// Entity → response map. Automapping binds every same-named member, and registering the element
/// map here is what lets <c>UserResponse.Language</c> bind automatically.
/// </summary>
public class LanguageResponseProfile : MapperProfile
{
    public LanguageResponseProfile()
    {
        CreateMap<Language, LanguageViewDto>();
    }
}
