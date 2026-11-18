using Aptison.Booking.Application.Features.Staff.Common.Models;


namespace Aptison.Booking.Application.Features.Staff.Common.Responses;

/// <summary>
/// The drawer's member. <c>CanManage</c> is false when the member holds a permission here the caller
/// does not; <c>CanDelete</c> is false once the member is on any appointment or review.
/// </summary>
public record StaffMemberResponse(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string DisplayName,
    string Initials,
    string? Email,
    string? PhoneNumber,
    string? AvatarUrl,
    string? JobTitleCode,
    bool IsBookable,
    bool IsActive,
    DateOnly JoinedAt,
    IReadOnlyList<StaffRoleViewDto> Roles,
    IReadOnlyList<StaffMemberServiceViewDto> Services,
    IReadOnlyList<StaffWorkHourViewDto> WorkHours,
    StaffStatsViewDto Stats,
    IReadOnlyList<string> BookingBlockers,
    bool IsSelf,
    bool CanManage,
    bool CanDelete,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
