using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Services.Common.Profiles;

public class ServiceResponseProfile : MapperProfile
{
    public ServiceResponseProfile()
    {
        CreateMap<ServiceTranslation, ServiceTranslationViewDto>()
            .For(dto => dto.LanguageCode, translation => translation.Language.Code);

        CreateMap<StaffService, ServiceStaffViewDto>();

        CreateMap<Service, ServiceResponse>()
            .For(dto => dto.Translations, service => service.Translations.OrderBy(translation => translation.Language.Code))
            // Through the staff member so a deleted one's assignment is left out, as in the grid.
            .For(dto => dto.Staff, service => service.StaffServices
                .OrderBy(staffService => staffService.StaffMember.User.FirstName)
                .ThenBy(staffService => staffService.StaffMember.User.LastName)
                .ThenBy(staffService => staffService.Id))
            .For(dto => dto.TimesBooked, service => service.AppointmentServices
                .Count(line => line.Appointment.Status != AppointmentStatus.Cancelled))
            .For(dto => dto.CanDelete, service => !service.AppointmentServices.Any())
            .For(dto => dto.UpdatedAt, service => (DateTimeOffset?)service.UpdatedAt);
    }
}
