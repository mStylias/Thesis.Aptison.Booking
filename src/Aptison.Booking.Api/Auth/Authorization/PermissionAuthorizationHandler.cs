using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Aptison.Booking.Api.Auth.Authorization;

/// <summary>
/// Decides every permission check, and does no I/O to do it: the set was resolved once by
/// <c>CurrentAppUserMiddleware</c>, so this is two lookups.
/// </summary>
internal sealed class PermissionAuthorizationHandler(ICurrentPermissions permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // The superadmin bypass: a permission added tomorrow works for platform administrators
        // immediately, rather than excluding them until role_permissions is backfilled.
        if (permissions.IsPlatformAdministrator || requirement.Codes.Any(permissions.Has))
        {
            context.Succeed(requirement);
        }

        // No Fail(): leaving the requirement unmet lets any other handler for it succeed.
        return Task.CompletedTask;
    }
}
