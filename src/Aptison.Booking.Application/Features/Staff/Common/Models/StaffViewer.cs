using Aptison.Booking.Application.Abstractions.Auth.Authorization;

namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>Who is looking: <c>IsSelf</c> and <c>CanManage</c> depend on it.</summary>
internal sealed record StaffViewer(
    Guid CompanyId,
    Guid CallerUserId,
    ICurrentPermissions Permissions,
    IPermissionResolver PermissionResolver,
    string? PublicBaseUrl,
    DateTime UtcNow);
