namespace Aptison.Booking.Application.Features.Availability.Common.Models;

internal sealed record CompanyAvailabilityInput(
    Guid CompanyId,
    TimeZoneInfo TimeZone,
    int SlotIntervalMinutes,
    int MinLeadTimeMinutes,
    IReadOnlyList<WeeklyWindow> BusinessHours,
    IReadOnlyList<HolidayWindow> Holidays,
    IReadOnlyList<StaffAvailabilityInput> Staff);
