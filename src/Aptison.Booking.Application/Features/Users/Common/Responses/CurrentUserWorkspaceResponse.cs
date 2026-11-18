using Aptison.Booking.Application.Features.Users.Common.Models;


namespace Aptison.Booking.Application.Features.Users.Common.Responses;

/// <summary>
/// Who the caller is at work. <c>Company</c> is null for a customer, a deactivated barber and a
/// superadmin who named no company; <c>Permissions</c> are then the global codes and <c>Staff</c> is
/// empty. Sorted, so two responses compare. No profile: it comes from several sources.
/// </summary>
public record CurrentUserWorkspaceResponse(
    WorkspaceCompanyViewDto? Company,
    IReadOnlyList<string> Permissions,
    bool IsPlatformAdmin,
    IReadOnlyList<WorkspaceStaffMemberViewDto> Staff);
