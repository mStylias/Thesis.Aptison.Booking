using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Analytics.Common.Readers;

/// <summary>Working time from the schedule, by the same rules the availability index uses.</summary>
internal static class AnalyticsScheduleReader
{
    /// <summary>
    /// Minutes each active, bookable staff member could work in the range: their work hours inside
    /// the shop's open hours, minus time off, from the day they joined. Inactive staff have none,
    /// since the date they left is not recorded.
    /// </summary>
    public static async Task<Dictionary<Guid, int>> ReadAvailableMinutesAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var scope = facts.Scope;
        var unitOfWork = facts.UnitOfWork;

        var staffMembers = await unitOfWork.GetRepository<StaffMember>()
            .QuerySelect(
                member => new { member.Id, member.JoinedAt },
                query => query.Where(member => member.IsActive && member.IsBookable))
            .ToListAsync(cancellationToken);

        var staffMemberIds = staffMembers.Select(member => member.Id).ToList();

        var workHours = await unitOfWork.GetRepository<StaffWorkHour>()
            .QuerySelect(
                hour => new { hour.StaffMemberId, hour.DayOfWeek, hour.StartTime, hour.EndTime },
                query => query.Where(hour => staffMemberIds.Contains(hour.StaffMemberId)))
            .ToListAsync(cancellationToken);

        // A day either side, so every local day in the range sees the absences touching it.
        var fromUtc = scope.From.AddDays(-1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = scope.To.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var timeOff = await unitOfWork.GetRepository<StaffTimeOff>()
            .QuerySelect(
                absence => new { absence.StaffMemberId, absence.StartsAt, absence.EndsAt },
                query => query.Where(absence => staffMemberIds.Contains(absence.StaffMemberId)
                                                && absence.StartsAt < toUtc && absence.EndsAt > fromUtc))
            .ToListAsync(cancellationToken);

        var businessHours = await ReadBusinessHoursAsync(unitOfWork, cancellationToken);
        var holidays = await ReadHolidaysAsync(unitOfWork, scope.From, scope.To, cancellationToken);

        var workHoursByStaff = workHours.ToLookup(
            hour => hour.StaffMemberId, hour => new WeeklyWindow(hour.DayOfWeek, hour.StartTime, hour.EndTime));
        var timeOffByStaff = timeOff.ToLookup(
            absence => absence.StaffMemberId, absence => new BusyWindow(absence.StartsAt, absence.EndsAt));

        var minutesByStaff = new Dictionary<Guid, int>();

        foreach (var member in staffMembers)
        {
            var memberWorkHours = workHoursByStaff[member.Id].ToList();
            var memberTimeOff = timeOffByStaff[member.Id].ToList();
            var joinedOn = DateOnly.FromDateTime(ShopTime.ToLocal(member.JoinedAt, scope.TimeZone));
            var minutes = 0d;

            for (var day = joinedOn > scope.From ? joinedOn : scope.From; day <= scope.To; day = day.AddDays(1))
            {
                var openWindows = ShopHours.GetOpenWindows(businessHours, holidays, day);

                minutes += AvailabilityCalculator
                    .GetFreeWindows(scope.TimeZone, openWindows, memberWorkHours, memberTimeOff, day)
                    .Sum(window => (window.End - window.Start).TotalMinutes);
            }

            minutesByStaff[member.Id] = (int)Math.Round(minutes);
        }

        return minutesByStaff;
    }

    /// <summary>The first hour the shop opens and the last hour it is open on any weekday; null when it has no hours.</summary>
    public static async Task<(int First, int Last)?> ReadOpenHoursAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var businessHours = await ReadBusinessHoursAsync(facts.UnitOfWork, cancellationToken);

        if (businessHours.Count == 0)
        {
            return null;
        }

        // A shop closing at 20:00 is open in the 19:00 hour and not the 20:00 one.
        return (
            businessHours.Min(hours => hours.Start.Hour),
            businessHours.Max(hours => hours.End == TimeOnly.MinValue ? 23 : hours.End.AddMinutes(-1).Hour));
    }

    private static async Task<List<WeeklyWindow>> ReadBusinessHoursAsync(
        IUnitOfWork<BookingContext> unitOfWork, CancellationToken cancellationToken)
    {
        var hours = await unitOfWork.GetRepository<CompanyBusinessHour>()
            .QuerySelect(hour => new { hour.DayOfWeek, hour.OpenTime, hour.CloseTime })
            .ToListAsync(cancellationToken);

        return [.. hours.Select(hour => new WeeklyWindow(hour.DayOfWeek, hour.OpenTime, hour.CloseTime))];
    }

    private static async Task<List<HolidayWindow>> ReadHolidaysAsync(
        IUnitOfWork<BookingContext> unitOfWork, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var holidays = await unitOfWork.GetRepository<CompanyHoliday>()
            .QuerySelect(
                holiday => new
                {
                    holiday.StartsOn, holiday.EndsOn, holiday.IsClosed, holiday.OverrideOpenTime, holiday.OverrideCloseTime
                },
                query => query.Where(holiday => holiday.StartsOn <= to && holiday.EndsOn >= from))
            .ToListAsync(cancellationToken);

        return [.. holidays.Select(holiday => new HolidayWindow(
            holiday.StartsOn, holiday.EndsOn, holiday.IsClosed, holiday.OverrideOpenTime, holiday.OverrideCloseTime))];
    }
}
