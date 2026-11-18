using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>The shop's and its barbers' hours, as the availability rules take them. Relies on the ownership filter.</summary>
internal static class ShopScheduleReader
{
    public static async Task<List<WeeklyWindow>> ReadBusinessHoursAsync(
        IRepository<CompanyBusinessHour, BookingContext> businessHourRepo, CancellationToken cancellationToken)
    {
        var businessHours = await businessHourRepo
            .QuerySelect(hour => new { hour.DayOfWeek, hour.OpenTime, hour.CloseTime })
            .ToListAsync(cancellationToken);

        return [.. businessHours.Select(hour => new WeeklyWindow(hour.DayOfWeek, hour.OpenTime, hour.CloseTime))];
    }

    public static async Task<List<HolidayWindow>> ReadHolidaysAsync(
        IRepository<CompanyHoliday, BookingContext> holidayRepo, DateOnly localDate, CancellationToken cancellationToken)
    {
        var holidays = await holidayRepo
            .QuerySelect(
                holiday => new
                {
                    holiday.StartsOn,
                    holiday.EndsOn,
                    holiday.IsClosed,
                    holiday.OverrideOpenTime,
                    holiday.OverrideCloseTime
                },
                query => query.Where(holiday => holiday.StartsOn <= localDate && holiday.EndsOn >= localDate))
            .ToListAsync(cancellationToken);

        return [.. holidays.Select(holiday => new HolidayWindow(
            holiday.StartsOn, holiday.EndsOn, holiday.IsClosed, holiday.OverrideOpenTime, holiday.OverrideCloseTime))];
    }

    /// <summary>
    /// The shop's open windows on one day, read live because the index lags a schedule change. Filters on
    /// the company itself, so it stays right under <c>BecomeAllOwners</c>.
    /// </summary>
    public static async Task<List<LocalWindow>> ReadOpenWindowsAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid companyId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var dayOfWeek = (short)localDate.DayOfWeek;

        var businessHours = await unitOfWork.GetRepository<CompanyBusinessHour>()
            .QuerySelect(
                hour => new { hour.DayOfWeek, hour.OpenTime, hour.CloseTime },
                query => query.Where(hour => hour.OwnerId == companyId && hour.DayOfWeek == dayOfWeek))
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
                    holiday.OwnerId == companyId && holiday.StartsOn <= localDate && holiday.EndsOn >= localDate))
            .ToListAsync(cancellationToken);

        return ShopHours.GetOpenWindows(
            [.. businessHours.Select(hour => new WeeklyWindow(hour.DayOfWeek, hour.OpenTime, hour.CloseTime))],
            [.. holidays.Select(holiday => new HolidayWindow(
                holiday.StartsOn, holiday.EndsOn, holiday.IsClosed, holiday.OverrideOpenTime, holiday.OverrideCloseTime))],
            localDate);
    }

    /// <summary>The barbers' hours on the weekday of <paramref name="localDate"/>, keyed by staff member.</summary>
    public static async Task<ILookup<Guid, WeeklyWindow>> ReadWorkHoursAsync(
        IRepository<StaffWorkHour, BookingContext> workHourRepo,
        IReadOnlyCollection<Guid> staffMemberIds,
        DateOnly localDate,
        CancellationToken cancellationToken)
    {
        // Stored 0 = Sunday, as DayOfWeek counts.
        var dayOfWeek = (short)localDate.DayOfWeek;

        var workHours = await workHourRepo
            .QuerySelect(
                hour => new { hour.StaffMemberId, hour.StartTime, hour.EndTime },
                query => query.Where(hour => staffMemberIds.Contains(hour.StaffMemberId) && hour.DayOfWeek == dayOfWeek))
            .ToListAsync(cancellationToken);

        return workHours.ToLookup(
            hour => hour.StaffMemberId, hour => new WeeklyWindow(dayOfWeek, hour.StartTime, hour.EndTime));
    }
}
