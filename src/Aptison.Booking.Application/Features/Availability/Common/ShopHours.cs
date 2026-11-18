using Aptison.Booking.Application.Features.Availability.Common.Models;

namespace Aptison.Booking.Application.Features.Availability.Common;

/// <summary>When a shop is open on one day. The availability index and the staff booking check both read this.</summary>
internal static class ShopHours
{
    /// <summary>A closed holiday removes the day, an override replaces it, and otherwise the weekly schedule stands.</summary>
    public static List<LocalWindow> GetOpenWindows(
        IReadOnlyList<WeeklyWindow> businessHours, IReadOnlyList<HolidayWindow> holidays, DateOnly day)
    {
        foreach (var holiday in holidays)
        {
            if (day < holiday.StartsOn || day > holiday.EndsOn)
            {
                continue;
            }

            if (holiday.IsClosed || holiday.OverrideOpen is null || holiday.OverrideClose is null)
            {
                return [];
            }

            return [new LocalWindow(holiday.OverrideOpen.Value, holiday.OverrideClose.Value)];
        }

        return businessHours
            .Where(hours => hours.DayOfWeek == (int)day.DayOfWeek)
            .Select(hours => new LocalWindow(hours.Start, hours.End))
            .ToList();
    }

    /// <summary>Whether the span from <paramref name="localStart"/> lies inside one window. No window crosses midnight.</summary>
    public static bool IsInsideOpenWindows(IReadOnlyList<LocalWindow> openWindows, DateTime localStart, int minutes)
    {
        var localEnd = localStart.AddMinutes(minutes);

        if (localEnd.Date != localStart.Date)
        {
            return false;
        }

        var start = TimeOnly.FromDateTime(localStart);
        var end = TimeOnly.FromDateTime(localEnd);

        return openWindows.Any(window => window.Start <= start && window.End >= end);
    }
}
