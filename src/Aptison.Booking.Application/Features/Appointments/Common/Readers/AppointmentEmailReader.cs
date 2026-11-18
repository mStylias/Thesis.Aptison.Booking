using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Entities.Users;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

internal static class AppointmentEmailReader
{
    /// <summary>
    /// Null when the customer has no email address, which a walk-in may not — unless
    /// <paramref name="requireEmail"/> is false, for a push, which leaves <c>Email</c> empty instead.
    /// </summary>
    public static async Task<AppointmentSummary?> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid appointmentId,
        CancellationToken cancellationToken,
        bool requireEmail = true)
    {
        var appointment = await unitOfWork.GetRepository<Appointment>()
            .QuerySelect(
                row => new
                {
                    row.OwnerId,
                    row.CustomerId,
                    row.StaffMemberId,
                    row.StartsAt,
                    row.EndsAt,
                    row.TotalPrice,
                    row.Currency,
                    row.Notes,
                    row.CalendarSequence
                },
                query => query.Where(row => row.Id == appointmentId))
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return null;
        }

        var customer = await unitOfWork.GetRepository<User>()
            .QuerySelect(
                user => new { user.Email, user.PreferredLanguageId, HasAccount = user.KeycloakId != null },
                query => query.Where(user => user.Id == appointment.CustomerId))
            .FirstOrDefaultAsync(cancellationToken);

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), appointment.OwnerId, cancellationToken);

        if (customer is null || company is null || (requireEmail && customer.Email is null))
        {
            return null;
        }

        var languageCode = await unitOfWork.GetRepository<Language>()
            .QuerySelect(language => language.Code, query => query.Where(language => language.Id == customer.PreferredLanguageId))
            .FirstOrDefaultAsync(cancellationToken);

        var companyText = await AppointmentViewReader.ReadCompanyTextAsync(
            unitOfWork.GetRepository<CompanyTranslation>(), company.Id, customer.PreferredLanguageId, cancellationToken);

        var staffDisplayName = await AppointmentViewReader.ReadStaffDisplayNameAsync(
            unitOfWork.GetRepository<StaffMember>(), appointment.StaffMemberId, cancellationToken);

        var lines = await unitOfWork.GetRepository<AppointmentService>()
            .QuerySelect(
                line => new { line.Id, line.ServiceId, line.PriceAtBooking, line.DurationAtBookingMinutes },
                query => query.Where(line => line.AppointmentId == appointmentId))
            .ToListAsync(cancellationToken);

        var serviceNamesByIds = await AppointmentViewReader.ReadServiceNamesAsync(
            unitOfWork.GetRepository<ServiceTranslation>(),
            [.. lines.Select(line => line.ServiceId).Distinct()],
            customer.PreferredLanguageId,
            cancellationToken);

        return new AppointmentSummary(
            appointmentId,
            customer.Email ?? string.Empty,
            languageCode ?? string.Empty,
            customer.HasAccount,
            companyText.Name,
            company.Slug,
            companyText.Address,
            company.Phone,
            company.Location.Y,
            company.Location.X,
            company.Timezone,
            staffDisplayName,
            appointment.StartsAt,
            (int)(appointment.EndsAt - appointment.StartsAt).TotalMinutes,
            [.. lines
                .OrderBy(line => line.Id)
                .Select(line => new BookedServiceLine(
                    serviceNamesByIds.GetValueOrDefault(line.ServiceId) ?? string.Empty,
                    line.PriceAtBooking,
                    line.DurationAtBookingMinutes))],
            appointment.TotalPrice,
            appointment.Currency,
            appointment.Notes,
            appointment.CalendarSequence);
    }
}
