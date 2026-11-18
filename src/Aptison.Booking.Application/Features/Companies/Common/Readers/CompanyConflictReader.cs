using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Companies.Common.Readers;

/// <summary>The shop's upcoming bookings a schedule write leaves outside its open time. Reported, never moved.</summary>
internal static class CompanyConflictReader
{
    /// <summary>Every upcoming booking, judged against the week and the holidays as they now stand.</summary>
    public static async Task<List<CompanyConflictViewDto>> ReadOutsideHoursAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IReadOnlyList<WeeklyWindow> businessHours,
        TimeZoneInfo timeZone,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(ShopTime.ToLocal(utcNow, timeZone));
        var holidays = await ReadHolidaysAsync(unitOfWork, today, cancellationToken);
        var appointments = await ReadUpcomingAsync(unitOfWork, utcNow, untilUtc: null, cancellationToken);

        return GetConflicts(appointments, businessHours, holidays, timeZone);
    }

    /// <summary>The bookings on the holiday's days: all of them on a closure, those outside the window on special hours.</summary>
    public static async Task<List<CompanyConflictViewDto>> ReadInsideHolidayAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        HolidayWindow holiday,
        TimeZoneInfo timeZone,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var fromUtc = ShopTime.ToUtcSkippingGap(holiday.StartsOn.ToDateTime(TimeOnly.MinValue), timeZone);
        var untilUtc = ShopTime.ToUtcSkippingGap(holiday.EndsOn.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

        var appointments = await ReadUpcomingAsync(
            unitOfWork, fromUtc > utcNow ? fromUtc : utcNow, untilUtc, cancellationToken);

        return GetConflicts(appointments, [], [holiday], timeZone);
    }

    private static List<CompanyConflictViewDto> GetConflicts(
        List<CompanyUpcomingAppointmentDto> appointments,
        IReadOnlyList<WeeklyWindow> businessHours,
        IReadOnlyList<HolidayWindow> holidays,
        TimeZoneInfo timeZone) =>
        [.. appointments
            .Where(appointment => !IsInsideOpenTime(appointment, businessHours, holidays, timeZone))
            .Select(appointment => ToView(appointment, timeZone))];

    private static async Task<List<HolidayWindow>> ReadHolidaysAsync(
        IUnitOfWork<BookingContext> unitOfWork, DateOnly today, CancellationToken cancellationToken)
    {
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
                query => query.Where(holiday => holiday.EndsOn >= today))
            .ToListAsync(cancellationToken);

        return [.. holidays.Select(holiday => new HolidayWindow(
            holiday.StartsOn, holiday.EndsOn, holiday.IsClosed, holiday.OverrideOpenTime, holiday.OverrideCloseTime))];
    }

    private static Task<List<CompanyUpcomingAppointmentDto>> ReadUpcomingAsync(
        IUnitOfWork<BookingContext> unitOfWork, DateTime fromUtc, DateTime? untilUtc, CancellationToken cancellationToken)
    {
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        return unitOfWork.GetRepository<Appointment>()
            .QueryDto<CompanyUpcomingAppointmentDto>(
                query => query
                    .Where(appointment =>
                        activeStatuses.Contains(appointment.Status)
                        && appointment.StartsAt > fromUtc
                        && (untilUtc == null || appointment.StartsAt < untilUtc))
                    .OrderBy(appointment => appointment.StartsAt)
                    .ThenBy(appointment => appointment.Id))
            .ToListAsync(cancellationToken);
    }

    // One that crosses local midnight fits no single day's window.
    private static bool IsInsideOpenTime(
        CompanyUpcomingAppointmentDto appointment,
        IReadOnlyList<WeeklyWindow> businessHours,
        IReadOnlyList<HolidayWindow> holidays,
        TimeZoneInfo timeZone)
    {
        var localStart = ShopTime.ToLocal(appointment.StartsAt, timeZone);
        var localEnd = ShopTime.ToLocal(appointment.EndsAt, timeZone);

        if (localEnd.Date != localStart.Date)
        {
            return false;
        }

        var start = TimeOnly.FromDateTime(localStart);
        var end = TimeOnly.FromDateTime(localEnd);

        return ShopHours.GetOpenWindows(businessHours, holidays, DateOnly.FromDateTime(localStart))
            .Any(window => window.Start <= start && window.End >= end);
    }

    private static CompanyConflictViewDto ToView(CompanyUpcomingAppointmentDto appointment, TimeZoneInfo timeZone)
    {
        var localStart = ShopTime.ToLocal(appointment.StartsAt, timeZone);
        var localEnd = ShopTime.ToLocal(appointment.EndsAt, timeZone);

        return new CompanyConflictViewDto(
            appointment.Id,
            DateOnly.FromDateTime(localStart),
            BookingFormats.FormatTimeOfDay(TimeOnly.FromDateTime(localStart)),
            BookingFormats.FormatTimeOfDay(TimeOnly.FromDateTime(localEnd)),
            appointment.CustomerDisplayName,
            appointment.StaffMemberDisplayName);
    }
}
