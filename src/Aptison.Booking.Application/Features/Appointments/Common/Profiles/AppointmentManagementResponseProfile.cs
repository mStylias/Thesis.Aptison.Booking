using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Profiles;

public class AppointmentManagementResponseProfile : MapperProfile
{
    public AppointmentManagementResponseProfile()
    {
        CreateMap<Appointment, OverlappingAppointmentViewDto>()
            .For(dto => dto.CustomerDisplayName, appointment =>
                (appointment.Customer.FirstName + " " + appointment.Customer.LastName).Trim())
            .For(dto => dto.StaffDisplayName,
                StaffMemberNames.BuildDisplayNameSelector<Appointment>(appointment => appointment.StaffMember.User))
            .For(dto => dto.LocalStartTime, appointment =>
                appointment.LocalStartTime.Hour.ToString().PadLeft(2, '0') + ":"
                + appointment.LocalStartTime.Minute.ToString().PadLeft(2, '0'))
            .For(dto => dto.LocalEndTime, appointment =>
                appointment.LocalEndTime.Hour.ToString().PadLeft(2, '0') + ":"
                + appointment.LocalEndTime.Minute.ToString().PadLeft(2, '0'));
    }
}
