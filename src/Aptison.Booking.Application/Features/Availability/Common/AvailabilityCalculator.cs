using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Domain.Entities.Companies;

namespace Aptison.Booking.Application.Features.Availability.Common;

/// <summary>
/// Turns a company's schedule into the instants something can be booked at. Pure — no I/O, no
/// clock of its own — because it is the only definition of "available" in the system and the one
/// thing worth testing directly.
/// </summary>
internal static class AvailabilityCalculator
{
    public static List<CompanyAvailabilitySlot> Build(
        CompanyAvailabilityInput company, DateOnly fromDay, DateOnly toDay, DateTime utcNow)
    {
        var slots = new List<CompanyAvailabilitySlot>();
        var earliestStart = utcNow.AddMinutes(company.MinLeadTimeMinutes);

        for (var day = fromDay; day <= toDay; day = day.AddDays(1))
        {
            var openWindows = ShopHours.GetOpenWindows(company.BusinessHours, company.Holidays, day);

            if (openWindows.Count == 0)
            {
                continue;
            }

            foreach (var staff in company.Staff)
            {
                // No services means nothing they can perform, so no slot has a length that fits.
                if (staff.ShortestOfferingMinutes <= 0)
                {
                    continue;
                }

                AddStaffSlots(company, staff, day, openWindows, earliestStart, slots);
            }
        }

        return slots;
    }

    private static void AddStaffSlots(
        CompanyAvailabilityInput company,
        StaffAvailabilityInput staff,
        DateOnly day,
        List<LocalWindow> openWindows,
        DateTime earliestStart,
        List<CompanyAvailabilitySlot> slots)
    {
        foreach (var segment in GetFreeWindows(company.TimeZone, openWindows, staff.WorkHours, staff.Busy, day))
        {
            AddSegmentSlots(company, staff, day, segment, earliestStart, slots);
        }
    }

    private static void AddSegmentSlots(
        CompanyAvailabilityInput company,
        StaffAvailabilityInput staff,
        DateOnly day,
        LocalWindow segment,
        DateTime earliestStart,
        List<CompanyAvailabilitySlot> slots)
    {
        var segmentEnd = ToMinutes(segment.End);
        var interval = company.SlotIntervalMinutes;

        // Aligned to the slot grid from local midnight, not to the segment's own start: a customer
        // expects 09:00 and 09:15, and an appointment ending at 09:07 must not shift the whole day.
        var firstStart = ((ToMinutes(segment.Start) + interval - 1) / interval) * interval;

        for (var minute = firstStart; minute + staff.ShortestOfferingMinutes <= segmentEnd; minute += interval)
        {
            var localStart = day.ToDateTime(new TimeOnly(minute / 60, minute % 60));

            // Only reachable on a spring-forward day, and only for a shop open at 03:00.
            if (company.TimeZone.IsInvalidTime(localStart))
            {
                continue;
            }

            var startsAt = TimeZoneInfo.ConvertTimeToUtc(localStart, company.TimeZone);

            if (startsAt < earliestStart)
            {
                continue;
            }

            slots.Add(new CompanyAvailabilitySlot
            {
                CompanyId = company.CompanyId,
                StaffMemberId = staff.StaffMemberId,
                StartsAt = startsAt,
                LocalDay = day,
                LocalMinute = (short)minute,
                // To the end of the free segment, uncapped: a booking of several services is
                // longer than any one of them, and a cap at the longest single service would hide
                // every slot that could take the chain.
                MaxDurationMinutes = (short)(segmentEnd - minute)
            });
        }
    }

    /// <summary>The staff member's work hours on the day, inside the shop's open windows, minus the busy windows.</summary>
    internal static List<LocalWindow> GetFreeWindows(
        TimeZoneInfo timeZone,
        List<LocalWindow> openWindows,
        IReadOnlyList<WeeklyWindow> workHours,
        IReadOnlyList<BusyWindow> busy,
        DateOnly day)
    {
        var working = Intersect(
            openWindows,
            workHours.Where(hours => hours.DayOfWeek == (int)day.DayOfWeek)
                .Select(hours => new LocalWindow(hours.Start, hours.End))
                .ToList());

        // Subtraction happens in local time, so only the busy instants need converting.
        return working.Count == 0 ? working : Subtract(working, GetBusyWindows(timeZone, busy, day));
    }

    /// <summary>Time off and appointments, clipped to the day and expressed in local terms.</summary>
    private static List<LocalWindow> GetBusyWindows(TimeZoneInfo timeZone, IReadOnlyList<BusyWindow> busy, DateOnly day)
    {
        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var windows = new List<LocalWindow>();

        foreach (var window in busy)
        {
            var localStart = ToLocal(timeZone, window.StartsAt);
            var localEnd = ToLocal(timeZone, window.EndsAt);

            if (localEnd <= dayStart || localStart >= dayEnd)
            {
                continue;
            }

            // A multi-day absence contributes the part that falls on this day.
            windows.Add(new LocalWindow(
                localStart <= dayStart ? TimeOnly.MinValue : TimeOnly.FromDateTime(localStart),
                localEnd >= dayEnd ? TimeOnly.MaxValue : TimeOnly.FromDateTime(localEnd)));
        }

        return windows;
    }

    private static DateTime ToLocal(TimeZoneInfo timeZone, DateTime instant) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            instant.Kind == DateTimeKind.Utc ? instant : instant.ToUniversalTime(), timeZone);

    internal static List<LocalWindow> Intersect(List<LocalWindow> left, List<LocalWindow> right)
    {
        var overlaps = new List<LocalWindow>();

        foreach (var first in left)
        {
            foreach (var second in right)
            {
                var start = first.Start > second.Start ? first.Start : second.Start;
                var end = first.End < second.End ? first.End : second.End;

                if (start < end)
                {
                    overlaps.Add(new LocalWindow(start, end));
                }
            }
        }

        return overlaps;
    }

    private static List<LocalWindow> Subtract(List<LocalWindow> windows, List<LocalWindow> busy)
    {
        var remaining = windows;

        foreach (var taken in busy.OrderBy(window => window.Start))
        {
            var next = new List<LocalWindow>(remaining.Count + 1);

            foreach (var window in remaining)
            {
                if (taken.End <= window.Start || taken.Start >= window.End)
                {
                    next.Add(window);
                    continue;
                }

                if (window.Start < taken.Start)
                {
                    next.Add(new LocalWindow(window.Start, taken.Start));
                }

                if (taken.End < window.End)
                {
                    next.Add(new LocalWindow(taken.End, window.End));
                }
            }

            remaining = next;
        }

        return remaining;
    }

    private static int ToMinutes(TimeOnly time) => (time.Hour * 60) + time.Minute;
}
