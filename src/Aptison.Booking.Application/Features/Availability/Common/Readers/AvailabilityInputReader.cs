using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Availability.Common.Readers;

/// <summary>
/// Reads what <see cref="AvailabilityCalculator"/> needs for one company. Every table here is
/// required-owner, so a caller with no company scope reads under <c>BecomeAllOwners</c>.
/// </summary>
internal static class AvailabilityInputReader
{
    /// <param name="shortestMinutesByStaffIds">The staff to include, each with the shortest block a slot must fit.</param>
    /// <param name="excludedAppointmentId">An appointment read as if it were gone: the one a customer is moving.</param>
    /// <param name="includeAppointments">False reads the schedule as if nothing were booked, for a day's total capacity.</param>
    public static async Task<CompanyAvailabilityInput> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        TimeZoneInfo timeZone,
        int slotIntervalMinutes,
        int minLeadTimeMinutes,
        DateOnly fromDay,
        DateOnly toDay,
        IReadOnlyDictionary<Guid, int> shortestMinutesByStaffIds,
        Guid? excludedAppointmentId,
        bool includeAppointments,
        CancellationToken cancellationToken)
    {
        var staffIds = shortestMinutesByStaffIds.Keys.ToArray();

        var businessHours = await unitOfWork.GetRepository<CompanyBusinessHour>()
            .QuerySelect(
                hour => new { hour.DayOfWeek, hour.OpenTime, hour.CloseTime },
                query => query.Where(hour => hour.OwnerId == companyId))
            .ToListAsync(cancellationToken);

        var holidays = await unitOfWork.GetRepository<CompanyHoliday>()
            .QuerySelect(
                holiday => new
                {
                    holiday.StartsOn,
                    holiday.EndsOn,
                    holiday.IsClosed,
                    holiday.OverrideOpenTime,
                    holiday.OverrideCloseTime
                },
                query => query.Where(holiday =>
                    holiday.OwnerId == companyId && holiday.EndsOn >= fromDay && holiday.StartsOn <= toDay))
            .ToListAsync(cancellationToken);

        var workHours = await unitOfWork.GetRepository<StaffWorkHour>()
            .QuerySelect(
                hour => new { hour.StaffMemberId, hour.DayOfWeek, hour.StartTime, hour.EndTime },
                query => query.Where(hour => hour.OwnerId == companyId && staffIds.Contains(hour.StaffMemberId)))
            .ToListAsync(cancellationToken);

        // A day either side, so an absence or appointment straddling local midnight still clips
        // correctly once converted. Stamped UTC because these bound timestamptz columns, and Npgsql
        // rejects an Unspecified DateTime against one.
        var windowStart = DateTime.SpecifyKind(
            fromDay.AddDays(-1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var windowEnd = DateTime.SpecifyKind(
            toDay.AddDays(2).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var timeOff = await unitOfWork.GetRepository<StaffTimeOff>()
            .QuerySelect(
                absence => new { absence.StaffMemberId, absence.StartsAt, absence.EndsAt },
                query => query.Where(absence =>
                    absence.OwnerId == companyId
                    && staffIds.Contains(absence.StaffMemberId)
                    && absence.EndsAt >= windowStart
                    && absence.StartsAt <= windowEnd))
            .ToListAsync(cancellationToken);

        // The same predicate appointments_no_double_booking keys on. A status that occupies the
        // calendar but is missing from ActiveStatuses would be invisible here too.
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        var appointments = await unitOfWork.GetRepository<Appointment>()
            .QuerySelect(
                appointment => new { appointment.StaffMemberId, appointment.BlockedFrom, appointment.BlockedUntil },
                query => query.Where(appointment =>
                    includeAppointments
                    && appointment.OwnerId == companyId
                    && staffIds.Contains(appointment.StaffMemberId)
                    && activeStatuses.Contains(appointment.Status)
                    && appointment.BlockedUntil >= windowStart
                    && appointment.BlockedFrom <= windowEnd
                    && (excludedAppointmentId == null || appointment.Id != excludedAppointmentId)))
            .ToListAsync(cancellationToken);

        var staff = new List<StaffAvailabilityInput>(staffIds.Length);

        foreach (var (staffMemberId, shortestMinutes) in shortestMinutesByStaffIds)
        {
            var busy = timeOff
                .Where(absence => absence.StaffMemberId == staffMemberId)
                .Select(absence => new BusyWindow(absence.StartsAt, absence.EndsAt))
                .Concat(appointments
                    .Where(appointment => appointment.StaffMemberId == staffMemberId)
                    .Select(appointment => new BusyWindow(appointment.BlockedFrom, appointment.BlockedUntil)))
                .ToList();

            staff.Add(new StaffAvailabilityInput(
                staffMemberId,
                [.. workHours
                    .Where(hour => hour.StaffMemberId == staffMemberId)
                    .Select(hour => new WeeklyWindow(hour.DayOfWeek, hour.StartTime, hour.EndTime))],
                busy,
                shortestMinutes));
        }

        return new CompanyAvailabilityInput(
            companyId,
            timeZone,
            slotIntervalMinutes,
            minLeadTimeMinutes,
            [.. businessHours.Select(hour => new WeeklyWindow(hour.DayOfWeek, hour.OpenTime, hour.CloseTime))],
            [.. holidays.Select(holiday => new HolidayWindow(
                holiday.StartsOn, holiday.EndsOn, holiday.IsClosed,
                holiday.OverrideOpenTime, holiday.OverrideCloseTime))],
            staff);
    }
}
