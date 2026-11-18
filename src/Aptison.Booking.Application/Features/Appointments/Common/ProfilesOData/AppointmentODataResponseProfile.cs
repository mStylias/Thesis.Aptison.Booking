using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.ProfilesOData;

/// <summary>
/// The company's book as the calendar and the grid read it. Every <c>For</c> must stay
/// SQL-translatable: <c>$filter</c> and <c>$orderby</c> run against this projection. Service names
/// follow the <c>languageId</c> parameter and fall back to whichever language a service has.
/// </summary>
public class AppointmentODataResponseProfile : MapperProfile
{
    public AppointmentODataResponseProfile()
    {
        var languageId = Parameter<Guid?>("languageId");

        CreateMap<AppointmentService, AppointmentServiceODataViewDto>()
            .For(dto => dto.Name, line => line.Service.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.Name)
                .FirstOrDefault() ?? "")
            .For(dto => dto.CategoryId, line => line.Service.CategoryId)
            .For(dto => dto.DurationMinutes, line => line.DurationAtBookingMinutes);

        CreateMap<Appointment, AppointmentODataResponse>()
            .For(dto => dto.LocalStartTime, appointment =>
                appointment.LocalStartTime.Hour.ToString().PadLeft(2, '0') + ":"
                + appointment.LocalStartTime.Minute.ToString().PadLeft(2, '0'))
            .For(dto => dto.LocalEndTime, appointment =>
                appointment.LocalEndTime.Hour.ToString().PadLeft(2, '0') + ":"
                + appointment.LocalEndTime.Minute.ToString().PadLeft(2, '0'))
            .For(dto => dto.DurationMinutes, appointment => (int)(appointment.EndsAt - appointment.StartsAt).TotalMinutes)
            .For(dto => dto.StaffDisplayName,
                StaffMemberNames.BuildDisplayNameSelector<Appointment>(appointment => appointment.StaffMember.User))
            .For(dto => dto.CustomerDisplayName, appointment =>
                (appointment.Customer.FirstName + " " + appointment.Customer.LastName).Trim())
            .For(dto => dto.CustomerEmail, appointment => appointment.Customer.Email)
            .For(dto => dto.CustomerPhoneNumber, appointment => appointment.Customer.PhoneNumber
                ?? appointment.Customer.CompanyCustomers.Select(row => row.KeptPhoneNumber).FirstOrDefault())
            .For(dto => dto.CustomerHasNote, appointment =>
                appointment.Customer.CompanyCustomers.Any(row => row.Note != null))
            .For(dto => dto.CustomerIsBlocked, appointment =>
                appointment.Customer.Bans.Any(ban => ban.OwnerId != null && ban.LiftedAt == null))
            .For(dto => dto.CustomerNoShowCount, appointment =>
                appointment.Customer.Appointments.Count(visit => visit.Status == AppointmentStatus.NoShow))
            // Ordered by id, a v7 guid: the order the booking inserted them in.
            .For(dto => dto.Services, appointment => appointment.Services.OrderBy(line => line.Id))
            .For(dto => dto.HasNotes, appointment =>
                !string.IsNullOrWhiteSpace(appointment.Notes) || !string.IsNullOrWhiteSpace(appointment.InternalNotes))
            .For(dto => dto.Rating, appointment =>
                appointment.Review == null ? null : (short?)appointment.Review.Rating)
            // AutoMap converts DateTime to DateTimeOffset but does not lift that to the nullable pair.
            .For(dto => dto.UpdatedAt, appointment => (DateTimeOffset?)appointment.UpdatedAt)
            .For(dto => dto.CancelledAt, appointment => (DateTimeOffset?)appointment.CancelledAt)
            .For(dto => dto.CancelledByDisplayName,
                StaffMemberNames.BuildOptionalDisplayNameSelector<Appointment>(appointment => appointment.CancelledBy))
            .For(dto => dto.CancelledByCustomer, appointment =>
                appointment.CancelledById != null && appointment.CancelledById == appointment.CustomerId)
            .For(dto => dto.CancelledByNameNormalized, appointment =>
                appointment.CancelledBy == null ? null : appointment.CancelledBy.NameNormalized);
    }
}
