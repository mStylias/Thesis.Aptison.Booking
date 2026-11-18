using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.ProfilesOData;

/// <summary>
/// Entity → response maps for the book's filter lists. <c>Appointments</c>, <c>CompanyCustomers</c>
/// and <c>Bans</c> here mean this company's only while the ownership filter is on: under
/// <c>BecomeAllOwners</c> they would read every shop's.
/// </summary>
public class CalendarResponseProfile : MapperProfile
{
    public CalendarResponseProfile()
    {
        var languageId = Parameter<Guid?>("languageId");

        // Name and NameNormalized must order alike, or $search matches one translation and shows another.
        CreateMap<Service, ServiceOptionODataResponse>()
            .For(dto => dto.Name, service => service.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.Name)
                .FirstOrDefault() ?? "")
            .For(dto => dto.NameNormalized, service => service.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.NameNormalized)
                .FirstOrDefault() ?? "");

        CreateMap<User, CustomerOptionODataResponse>()
            .For(dto => dto.DisplayName, user => (user.FirstName + " " + user.LastName).Trim())
            .For(dto => dto.PhoneNumber, user =>
                user.PhoneNumber ?? user.CompanyCustomers.Select(row => row.KeptPhoneNumber).FirstOrDefault())
            .For(dto => dto.HasAccount, user => user.OwnerId == null)
            .For(dto => dto.LastAppointmentDate, user => user.Appointments.Max(appointment => appointment.LocalDate))
            .For(dto => dto.PreferredStaffMemberId, user =>
                user.CompanyCustomers.Select(row => row.PreferredStaffMemberId).FirstOrDefault())
            .For(dto => dto.PreferredStaffIsActive, user => user.CompanyCustomers
                .Where(row => row.PreferredStaffMember != null)
                .Select(row => (bool?)row.PreferredStaffMember!.IsActive)
                .FirstOrDefault())
            .For(dto => dto.IsBlocked, user => user.Bans.Any(ban => ban.OwnerId != null && ban.LiftedAt == null))
            .For(dto => dto.HasNote, user => user.CompanyCustomers.Any(row => row.Note != null));
    }
}
