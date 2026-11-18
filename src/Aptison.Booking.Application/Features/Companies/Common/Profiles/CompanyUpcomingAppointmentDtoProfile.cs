using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Companies.Common.Profiles;

internal class CompanyUpcomingAppointmentDtoProfile : MapperProfile
{
    public CompanyUpcomingAppointmentDtoProfile()
    {
        CreateMap<Appointment, CompanyUpcomingAppointmentDto>()
            .For(dto => dto.CustomerDisplayName,
                StaffMemberNames.BuildDisplayNameSelector<Appointment>(appointment => appointment.Customer))
            .For(dto => dto.StaffMemberDisplayName,
                StaffMemberNames.BuildDisplayNameSelector<Appointment>(appointment => appointment.StaffMember.User));
    }
}
