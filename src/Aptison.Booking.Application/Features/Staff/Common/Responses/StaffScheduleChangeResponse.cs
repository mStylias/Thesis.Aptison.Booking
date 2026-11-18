using Aptison.Booking.Application.Features.Staff.Common.Models;


namespace Aptison.Booking.Application.Features.Staff.Common.Responses;

/// <summary>
/// What an hours or time-off write stored, and the upcoming appointments it leaves outside the
/// member's hours or inside their time off. Nothing is moved.
/// </summary>
public record StaffScheduleChangeResponse(
    IReadOnlyList<StaffWorkHourViewDto>? WorkHours,
    StaffTimeOffResponse? TimeOff,
    IReadOnlyList<StaffConflictViewDto> Conflicts);
