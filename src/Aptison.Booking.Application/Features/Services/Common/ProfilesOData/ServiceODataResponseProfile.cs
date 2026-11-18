using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.ResponseOData;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Services;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Services.Common.ProfilesOData;

/// <summary>
/// The services grid. Every <c>For</c> must stay SQL-translatable: <c>$filter</c> and <c>$orderby</c>
/// run against this projection. Names follow the <c>languageId</c> parameter and fall back to the
/// first translation by language id.
/// </summary>
public class ServiceODataResponseProfile : MapperProfile
{
    public ServiceODataResponseProfile()
    {
        var languageId = Parameter<Guid?>("languageId");

        CreateMap<StaffService, ServiceStaffODataViewDto>()
            .For(dto => dto.DisplayName,
                StaffMemberNames.BuildDisplayNameSelector<StaffService>(staffService => staffService.StaffMember.User))
            .For(dto => dto.IsActive, staffService => staffService.StaffMember.IsActive);

        // Name, NameNormalized and Description must order alike, or $search matches one translation and shows another.
        CreateMap<Service, ServiceODataResponse>()
            .For(dto => dto.Name, service => service.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.Name)
                .FirstOrDefault() ?? "")
            .For(dto => dto.NameNormalized, service => service.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.NameNormalized)
                .FirstOrDefault() ?? "")
            .For(dto => dto.Description, service => service.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.Description)
                .FirstOrDefault())
            .For(dto => dto.CategoryName, service => service.Category == null
                ? null
                : service.Category.Translations
                    .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                    .ThenBy(translation => translation.LanguageId)
                    .Select(translation => translation.Name)
                    .FirstOrDefault())
            .For(dto => dto.Currency, service => service.Owner.Currency)
            .For(dto => dto.CalendarMinutes, service =>
                service.DurationMinutes + service.BufferBeforeMinutes + service.BufferAfterMinutes)
            .For(dto => dto.Staff, service => service.StaffServices
                .OrderBy(staffService => staffService.StaffMember.User.FirstName)
                .ThenBy(staffService => staffService.StaffMember.User.LastName)
                .ThenBy(staffService => staffService.Id))
            .For(dto => dto.StaffCount, service =>
                service.StaffServices.Count(staffService => staffService.StaffMember.IsActive))
            .For(dto => dto.MinPrice, service => service.StaffServices
                .Where(staffService => staffService.StaffMember.IsActive)
                .Min(staffService => (decimal?)(staffService.CustomPrice ?? service.Price)))
            .For(dto => dto.MaxPrice, service => service.StaffServices
                .Where(staffService => staffService.StaffMember.IsActive)
                .Max(staffService => (decimal?)(staffService.CustomPrice ?? service.Price)))
            .For(dto => dto.IsBookableOnline, service => service.IsActive && service.StaffServices
                .Any(staffService => staffService.StaffMember.IsActive && staffService.StaffMember.IsBookable))
            .For(dto => dto.TimesBooked, service => service.AppointmentServices
                .Count(line => line.Appointment.Status != AppointmentStatus.Cancelled))
            .For(dto => dto.LastBookedAt, service => (DateTimeOffset?)service.AppointmentServices
                .Where(line => line.Appointment.Status != AppointmentStatus.Cancelled)
                .Max(line => (DateTime?)line.Appointment.StartsAt))
            // AutoMap converts DateTime to DateTimeOffset but does not lift that to the nullable pair.
            .For(dto => dto.UpdatedAt, service => (DateTimeOffset?)service.UpdatedAt);
    }
}
