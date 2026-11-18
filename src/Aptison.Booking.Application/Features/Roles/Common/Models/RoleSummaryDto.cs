using Aptison.Booking.Application.Features.Roles.Common.Responses;

namespace Aptison.Booking.Application.Features.Roles.Common.Models;

/// <summary>The half of <see cref="RoleResponse"/> that projects straight from <c>roles</c>.</summary>
public record RoleSummaryDto(
    Guid Id,
    Guid? OwnerId,
    string Name,
    string? Description,
    bool IsSystem);
