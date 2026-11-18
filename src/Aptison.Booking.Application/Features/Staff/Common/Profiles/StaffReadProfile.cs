using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Staff.Common.Profiles;

/// <summary>Entity → internal projection maps for the staff reads.</summary>
internal class StaffReadProfile : MapperProfile
{
    public StaffReadProfile()
    {
        // Only read for a matched address, so the email is never null.
        CreateMap<User, StaffCandidate>()
            .For(candidate => candidate.UserId, user => user.Id)
            .For(candidate => candidate.Email, user => user.Email!);

        CreateMap<Role, StaffRoleViewDto>();

        CreateMap<Appointment, StaffUpcomingAppointmentDto>()
            .For(dto => dto.CustomerDisplayName,
                StaffMemberNames.BuildDisplayNameSelector<Appointment>(appointment => appointment.Customer));
    }
}
