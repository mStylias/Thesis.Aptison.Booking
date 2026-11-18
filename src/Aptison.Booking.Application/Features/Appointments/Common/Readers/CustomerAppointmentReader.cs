using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Features.Appointments.Common.Enums;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// A customer's own appointments as <see cref="AppointmentResponse"/>, across every shop. Booking,
/// reading back, listing, cancelling and moving all answer through here, so they answer alike.
/// </summary>
internal static class CustomerAppointmentReader
{
    /// <summary>
    /// <paramref name="shape"/> filters, orders and pages after the customer filter, which is what
    /// scopes the read once ownership is widened. An appointment whose shop row is gone is left out.
    /// </summary>
    public static async Task<List<AppointmentResponse>> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IUserContextScope userContext,
        Guid customerId,
        Func<IQueryable<Appointment>, IQueryable<Appointment>> shape,
        string? languageCode,
        string? publicBaseUrl,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // Appointments, their lines, reviews and staff rows are required-owner and the customer's
        // endpoints have no company scope, so each read would otherwise come back empty.
        using var _ = userContext.BecomeAllOwners();

        var appointments = await unitOfWork.GetRepository<Appointment>()
            .QueryDto<BookedAppointmentDto>(query =>
                shape(query.Where(appointment => appointment.CustomerId == customerId)))
            .ToListAsync(cancellationToken);

        if (appointments.Count == 0)
        {
            return [];
        }

        var appointmentIds = appointments.Select(appointment => appointment.Id).ToArray();
        var companyIds = appointments.Select(appointment => appointment.CompanyId).Distinct().ToArray();

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            unitOfWork.GetRepository<Language>(), languageCode, cancellationToken);

        // Without the active filter: an appointment outlives its shop being switched off.
        var companiesByIds = await unitOfWork.GetRepository<Company>()
            .QueryDto<BookingCompanyDto>(query => query.Where(company => companyIds.Contains(company.Id)))
            .ToDictionaryAsync(company => company.Id, cancellationToken);

        var companyTextsByIds = await AppointmentViewReader.ReadCompanyTextsAsync(
            unitOfWork.GetRepository<CompanyTranslation>(), companyIds, languageId, cancellationToken);

        var staffNamesByIds = await AppointmentViewReader.ReadStaffDisplayNamesAsync(
            unitOfWork.GetRepository<StaffMember>(),
            [.. appointments.Select(appointment => appointment.StaffMemberId).Distinct()],
            cancellationToken);

        var lines = await unitOfWork.GetRepository<AppointmentService>()
            .QuerySelect(
                line => new { line.AppointmentId, line.Id, line.ServiceId, line.PriceAtBooking, line.DurationAtBookingMinutes },
                query => query.Where(line => appointmentIds.Contains(line.AppointmentId)))
            .ToListAsync(cancellationToken);

        var serviceNamesByIds = await AppointmentViewReader.ReadServiceNamesAsync(
            unitOfWork.GetRepository<ServiceTranslation>(),
            [.. lines.Select(line => line.ServiceId).Distinct()],
            languageId,
            cancellationToken);

        // Ordered by id, a v7 guid: the order the booking inserted them in, and the order they are performed.
        var serviceLinesByAppointmentIds = lines
            .OrderBy(line => line.Id)
            .ToLookup(
                line => line.AppointmentId,
                line => new AppointmentServiceViewDto(
                    line.ServiceId,
                    serviceNamesByIds.GetValueOrDefault(line.ServiceId) ?? string.Empty,
                    line.PriceAtBooking,
                    line.DurationAtBookingMinutes));

        var responses = new List<AppointmentResponse>(appointments.Count);

        foreach (var appointment in appointments)
        {
            if (!companiesByIds.TryGetValue(appointment.CompanyId, out var company))
            {
                continue;
            }

            responses.Add(Build(
                appointment,
                company,
                companyTextsByIds.GetValueOrDefault(company.Id) ?? CompanyTextDto.Empty,
                staffNamesByIds.GetValueOrDefault(appointment.StaffMemberId) ?? string.Empty,
                [.. serviceLinesByAppointmentIds[appointment.Id]],
                publicBaseUrl,
                utcNow));
        }

        return responses;
    }

    private static AppointmentResponse Build(
        BookedAppointmentDto appointment,
        BookingCompanyDto company,
        CompanyTextDto companyText,
        string staffDisplayName,
        IReadOnlyList<AppointmentServiceViewDto> services,
        string? publicBaseUrl,
        DateTime utcNow)
    {
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(
            appointment.StartsAt, BookingCompanyReader.GetTimeZone(company.Timezone));

        var changeableUntil = CustomerAppointmentRules.GetChangeableUntil(
            appointment.Status, appointment.StartsAt, company.CancellationCutoffMinutes);

        var reviewableUntil = CustomerAppointmentRules.GetReviewableUntil(appointment.Status, appointment.EndsAt);

        AppointmentCanceller? cancelledBy = appointment.Status != AppointmentStatus.Cancelled
            ? null
            : appointment.CancelledByCustomer ? AppointmentCanceller.Customer : AppointmentCanceller.Shop;

        return new AppointmentResponse(
            appointment.Id,
            company.Id,
            company.Slug,
            companyText.Name,
            FileStorageUtils.BuildPublicUrl(company.LogoStorageKey, publicBaseUrl),
            companyText.Address,
            companyText.Street,
            companyText.Neighborhood,
            companyText.City,
            company.Location.Y,
            company.Location.X,
            company.Phone,
            company.Email,
            appointment.StaffMemberId,
            staffDisplayName,
            BookingFormats.FormatInstant(appointment.StartsAt),
            BookingFormats.FormatInstant(appointment.EndsAt),
            company.Timezone,
            BookingFormats.FormatDay(DateOnly.FromDateTime(localStart)),
            BookingFormats.FormatTimeOfDay((localStart.Hour * 60) + localStart.Minute),
            appointment.Status,
            cancelledBy,
            appointment.CancelledAt is { } cancelledAt ? BookingFormats.FormatInstant(cancelledAt) : null,
            services,
            appointment.TotalPrice,
            appointment.Currency,
            company.CancellationCutoffMinutes,
            appointment.Notes,
            changeableUntil is { } until ? BookingFormats.FormatInstant(until) : null,
            CustomerAppointmentRules.CanChange(
                appointment.Status, appointment.StartsAt, company.CancellationCutoffMinutes, utcNow),
            CustomerAppointmentRules.CanAddToCalendar(appointment.Status, appointment.StartsAt, utcNow),
            reviewableUntil is { } reviewUntil ? BookingFormats.FormatInstant(reviewUntil) : null,
            CustomerAppointmentRules.CanReview(
                appointment.Status, appointment.EndsAt, appointment.Review is not null, utcNow),
            appointment.Review);
    }
}
