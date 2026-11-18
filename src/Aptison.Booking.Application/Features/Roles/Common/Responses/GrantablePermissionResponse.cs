namespace Aptison.Booking.Application.Features.Roles.Common.Responses;

/// <summary>
/// A catalog row answered for one caller in one scope. <c>IsTenantGrantable</c> is whether a
/// company-owned role may contain it at all; <c>CanGrant</c> adds "and the caller holds it here",
/// mirroring the escalation rule the write endpoints enforce.
/// </summary>
public record GrantablePermissionResponse(
    string Code,
    string Name,
    string? Description,
    string Category,
    bool IsTenantGrantable,
    bool CanGrant);
