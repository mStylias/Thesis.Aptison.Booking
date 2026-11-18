using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Domain.Entities.Authorization;

namespace Aptison.Booking.Application.Features.Roles.Common.Rules;

/// <summary>
/// Drops the roles only a platform administrator may see. Applied to every role query a caller
/// reaches, so a hidden role is absent rather than refused — the same answer the ownership filter
/// already gives for another company's role.
/// <para>
/// Not an EF query filter. <c>PermissionStore</c> reads every role to build the global
/// role → permission map, so a global filter would drop a hidden role's permissions from the cache
/// and silently strip them from everyone holding it.
/// </para>
/// </summary>
internal static class RoleVisibility
{
    public static IQueryable<Role> Apply(IQueryable<Role> roles, ICurrentPermissions caller) =>
        caller.IsPlatformAdministrator ? roles : roles.Where(role => !role.IsPlatformOnly);
}
