using Aptison.Booking.Application.Features.Roles.Common.Profiles;

namespace Aptison.Booking.Application.Features.Roles.Common.Models;

/// <summary>
/// One catalog row, reduced to what a grant decision needs. Mapped from both the catalog and
/// <c>role_permissions</c> — see <see cref="CatalogPermissionProfile"/>.
/// </summary>
public record CatalogPermission(Guid Id, string Code, bool IsTenantGrantable);
