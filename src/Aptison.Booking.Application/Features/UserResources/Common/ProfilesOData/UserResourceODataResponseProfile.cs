using Aptison.Booking.Application.Features.UserResources.Common.ResponseOData;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.UserResources.Common.ProfilesOData;

public class UserResourceODataResponseProfile : MapperProfile
{
    public UserResourceODataResponseProfile()
    {
        CreateMap<UserResource, UserResourceODataResponse>();
    }
}
