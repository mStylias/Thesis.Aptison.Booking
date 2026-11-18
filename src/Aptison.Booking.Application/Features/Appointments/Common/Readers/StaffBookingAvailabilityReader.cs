using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>What a staff booking's live availability starts from. Relies on the ownership filter.</summary>
internal static class StaffBookingAvailabilityReader
{
    /// <summary>
    /// The minutes each bookable barber needs free: the selection's blocked length, or with no
    /// services the shortest thing they offer.
    /// </summary>
    public static async Task<Dictionary<Guid, int>> ReadRequiredMinutesAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        IReadOnlyList<Guid> serviceIds,
        Guid? staffMemberId,
        CancellationToken cancellationToken)
    {
        var offeringRepo = unitOfWork.GetRepository<CompanyServiceOffering>();

        var bookableStaffIds = await unitOfWork.GetRepository<StaffMember>()
            .QuerySelect(
                member => member.Id,
                query => query.Where(member =>
                    member.IsActive && member.IsBookable && (staffMemberId == null || member.Id == staffMemberId)))
            .ToHashSetAsync(cancellationToken);

        if (serviceIds.Count > 0)
        {
            var selection = await BookingSelectionReader.ReadAsync(
                offeringRepo, companyId, serviceIds, staffMemberId, cancellationToken);

            return selection.Candidates
                .Where(candidate => bookableStaffIds.Contains(candidate.StaffMemberId))
                .ToDictionary(candidate => candidate.StaffMemberId, candidate => candidate.BlockedMinutes);
        }

        // Spans companies, so the company filter is explicit.
        var offerings = await offeringRepo
            .QuerySelect(
                offering => new { offering.StaffMemberId, offering.RequiredMinutes },
                query => query.Where(offering =>
                    offering.CompanyId == companyId
                    && (staffMemberId == null || offering.StaffMemberId == staffMemberId)))
            .ToListAsync(cancellationToken);

        return offerings
            .Where(offering => bookableStaffIds.Contains(offering.StaffMemberId))
            .GroupBy(offering => offering.StaffMemberId)
            .ToDictionary(staff => staff.Key, staff => (int)staff.Min(offering => offering.RequiredMinutes));
    }

    /// <summary>
    /// The customer's other appointments at this shop, widened by the gap <c>CUSTOMER_OVERLAP</c>
    /// warns inside. A time offered here therefore never draws that warning.
    /// </summary>
    /// <param name="excludedAppointmentId">The appointment being edited, which is not one of their others.</param>
    public static async Task<List<BusyWindow>> ReadCustomerBusyAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid? customerId,
        DateTime windowStart,
        DateTime windowEnd,
        Guid? excludedAppointmentId,
        CancellationToken cancellationToken)
    {
        if (customerId is not { } id)
        {
            return [];
        }

        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        var appointments = await unitOfWork.GetRepository<Appointment>()
            .QuerySelect(
                appointment => new { appointment.StartsAt, appointment.EndsAt },
                query => query.Where(appointment =>
                    appointment.CustomerId == id
                    && activeStatuses.Contains(appointment.Status)
                    && appointment.EndsAt >= windowStart
                    && appointment.StartsAt <= windowEnd
                    && (excludedAppointmentId == null || appointment.Id != excludedAppointmentId)))
            .ToListAsync(cancellationToken);

        return [.. appointments.Select(appointment => new BusyWindow(
            appointment.StartsAt - AppointmentLimits.MinimumCustomerGap,
            appointment.EndsAt + AppointmentLimits.MinimumCustomerGap))];
    }
}
