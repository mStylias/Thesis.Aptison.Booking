using System.Linq.Expressions;
using Aptison.Booking.Application.Common.Expressions;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.ProfilesOData;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Profiles;

/// <summary>
/// The management drawer's appointment: <see cref="AppointmentODataResponseProfile"/>'s row plus the
/// notes, prices and audit. <c>CompanyCustomers</c>, <c>Bans</c> and <c>Appointments</c> are this
/// shop's only, through the ownership filter.
/// </summary>
public class AppointmentDetailsResponseProfile : MapperProfile
{
    public AppointmentDetailsResponseProfile()
    {
        var languageId = Parameter<Guid?>("languageId");

        CreateMap<AppointmentService, AppointmentDetailsServiceViewDto>()
            .For(dto => dto.Name, line => line.Service.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.Name)
                .FirstOrDefault() ?? "")
            .For(dto => dto.CategoryId, line => line.Service.CategoryId)
            .For(dto => dto.DurationMinutes, line => line.DurationAtBookingMinutes)
            .For(dto => dto.Price, line => line.PriceAtBooking);

        CreateMap<Appointment, AppointmentDetailsResponse>()
            .For(dto => dto.StaffDisplayName,
                StaffMemberNames.BuildDisplayNameSelector<Appointment>(appointment => appointment.StaffMember.User))
            .For(dto => dto.CustomerDisplayName, appointment =>
                (appointment.Customer.FirstName + " " + appointment.Customer.LastName).Trim())
            .For(dto => dto.CustomerEmail, appointment => appointment.Customer.Email)
            .For(dto => dto.CustomerPhoneNumber, appointment => appointment.Customer.PhoneNumber
                ?? appointment.Customer.CompanyCustomers.Select(row => row.KeptPhoneNumber).FirstOrDefault())
            // Not KeycloakId: the customers page and its edit and merge rules go by ownership.
            .For(dto => dto.CustomerHasAccount, appointment => appointment.Customer.OwnerId == null)
            .For(dto => dto.CustomerNote, appointment =>
                appointment.Customer.CompanyCustomers.Select(row => row.Note).FirstOrDefault())
            .For(dto => dto.CustomerPreferredStaffMemberId, appointment =>
                appointment.Customer.CompanyCustomers.Select(row => row.PreferredStaffMemberId).FirstOrDefault())
            .For(dto => dto.CustomerPreferredStaffDisplayName,
                StaffMemberNames.BuildOptionalDisplayNameSelector<Appointment>(appointment =>
                    appointment.Customer.CompanyCustomers.Select(row => row.PreferredStaffMember!.User).FirstOrDefault()))
            .For(dto => dto.CustomerPreferredStaffIsActive, appointment =>
                appointment.Customer.CompanyCustomers.Select(row => (bool?)row.PreferredStaffMember!.IsActive).FirstOrDefault())
            .For(dto => dto.CustomerNoShowCount, appointment =>
                appointment.Customer.Appointments.Count(visit => visit.Status == AppointmentStatus.NoShow))
            .For(dto => dto.CustomerBlock, appointment => appointment.Customer.Bans
                .Where(ban => ban.OwnerId != null && ban.LiftedAt == null)
                .OrderByDescending(ban => ban.BannedAt)
                .FirstOrDefault())
            .For(dto => dto.LocalStartTime, appointment =>
                appointment.LocalStartTime.Hour.ToString().PadLeft(2, '0') + ":"
                + appointment.LocalStartTime.Minute.ToString().PadLeft(2, '0'))
            .For(dto => dto.LocalEndTime, appointment =>
                appointment.LocalEndTime.Hour.ToString().PadLeft(2, '0') + ":"
                + appointment.LocalEndTime.Minute.ToString().PadLeft(2, '0'))
            .For(dto => dto.CreatedByDisplayName, BuildCreatedByDisplayName())
            .For(dto => dto.UpdatedAt, appointment => (DateTimeOffset?)appointment.UpdatedAt)
            .For(dto => dto.ModifiedByDisplayName,
                StaffMemberNames.BuildOptionalDisplayNameSelector<Appointment>(appointment => appointment.ModifiedBy))
            .For(dto => dto.CancelledAt, appointment => (DateTimeOffset?)appointment.CancelledAt)
            .For(dto => dto.CancelledByDisplayName,
                StaffMemberNames.BuildOptionalDisplayNameSelector<Appointment>(appointment => appointment.CancelledBy))
            .For(dto => dto.CancelledByCustomer, appointment =>
                appointment.CancelledById != null && appointment.CancelledById == appointment.CustomerId)
            // Ordered by id, a v7 guid: the order the booking inserted them in.
            .For(dto => dto.Services, appointment => appointment.Services.OrderBy(line => line.Id));
    }

    // No creator on rows the sample data inserted: every one of them is an online booking.
    private static Expression<Func<Appointment, string>> BuildCreatedByDisplayName()
    {
        var byCreator = StaffMemberNames.BuildOptionalDisplayNameSelector<Appointment>(appointment => appointment.CreatedBy);
        var byCustomer = StaffMemberNames.BuildDisplayNameSelector<Appointment>(appointment => appointment.Customer);
        var appointment = byCreator.Parameters[0];

        return Expression.Lambda<Func<Appointment, string>>(
            Expression.Coalesce(byCreator.Body, ExpressionUtils.Inline(byCustomer, appointment)), appointment);
    }
}
