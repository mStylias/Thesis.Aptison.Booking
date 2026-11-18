using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.UserResources.Common.Profiles;

public class UserResourceResponseProfile : MapperProfile
{
    public UserResourceResponseProfile()
    {
        CreateMap<UserResource, UserResourceResponse>();
    }
}
