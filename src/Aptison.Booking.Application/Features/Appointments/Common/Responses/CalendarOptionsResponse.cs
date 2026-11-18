using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>
/// What the management calendar reads once per company. Times are shop-local <c>HH:mm</c> and
/// <c>DayOfWeek</c> is ISO, <c>1</c> = Monday to <c>7</c> = Sunday. A weekday with no entry is closed.
/// </summary>
public record CalendarOptionsResponse(
    string Timezone,
    int SlotIntervalMinutes,
    IReadOnlyList<BusinessHourViewDto> BusinessHours,
    IReadOnlyList<CalendarStaffMemberViewDto> StaffMembers,
    IReadOnlyList<CalendarServiceCategoryViewDto> ServiceCategories);
