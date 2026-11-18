using Aptison.Booking.Application.Features.Roles.Common.Responses;

namespace Aptison.Booking.Application.Features.Roles.Common.Models;

/// <summary>
/// One <c>permissions</c> row, before any caller enters the picture.
/// <see cref="GrantablePermissionResponse"/> is this plus the members that depend on who is asking.
/// </summary>
public record PermissionCatalogDto(
    string Code,
    string Name,
    string? Description,
    string Category,
    bool IsTenantGrantable);
