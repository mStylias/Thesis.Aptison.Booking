using Aptison.Booking.Application.Features.Staff.Common.Models;


namespace Aptison.Booking.Application.Features.Staff.Common.Responses;

/// <summary>
/// One card of the staff page. Times are shop-local <c>HH:mm</c>, <c>...At</c> a UTC instant.
/// <c>BookingBlockers</c> holds <c>NOT_BOOKABLE</c>, <c>NO_SERVICES</c> or <c>NO_WORK_HOURS</c>, and
/// nothing for an inactive member.
/// </summary>
public record StaffMemberCardResponse(
    Guid Id,
    string FullName,
    string DisplayName,
    string Initials,
    string? AvatarUrl,
    string? JobTitleCode,
    bool IsActive,
    bool IsBookable,
    IReadOnlyList<string> RoleNames,
    int ServiceCount,
    StaffTodayViewDto Today,
    StaffTimeOffSpanViewDto? CurrentTimeOff,
    DateTimeOffset? NextAppointmentAt,
    decimal? Rating,
    int ReviewCount,
    IReadOnlyList<string> BookingBlockers);
