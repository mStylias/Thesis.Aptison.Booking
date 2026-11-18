using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Models;

namespace Aptison.Booking.Application.Features.Staff.Common.Responses;

/// <summary><c>DayOfWeek</c> is ISO, <c>1</c> = Monday to <c>7</c> = Sunday.</summary>
public record StaffOptionsResponse(
    string Timezone,
    int SlotIntervalMinutes,
    string Currency,
    IReadOnlyList<BusinessHourViewDto> BusinessHours,
    IReadOnlyList<string> JobTitles,
    IReadOnlyList<StaffRoleOptionViewDto> Roles,
    IReadOnlyList<StaffServiceOptionViewDto> Services);
