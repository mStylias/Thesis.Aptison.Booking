using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Reminders.Common.Profiles;

public class PushDeviceResponseProfile : MapperProfile
{
    public PushDeviceResponseProfile()
    {
        CreateMap<UserDevice, PushDeviceResponse>()
            .For(d => d.Label, s => s.DeviceName);
    }
}
