using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Common.Rules;

/// <summary>
/// The two content rules on <c>roles:create</c> and <c>roles:assign</c>, in the one place both write
/// paths reach — the authorization policy cannot see them, since it answers only "may you call this".
/// <list type="number">
/// <item>Some codes are never tenant-grantable: scoping a grant to a company does not scope the
/// authority the code confers.</item>
/// <item>A caller may only grant what they hold in <em>this request's scope</em>. Platform
/// administrators are exempt — the bypass means they hold everything by definition.</item>
/// </list>
/// </summary>
internal static class RoleGrantRules
{
    /// <summary>
    /// Looks the requested codes up in the catalog. An unknown code is a 400 naming the offenders,
    /// not a silent role granting less than the caller asked for.
    /// </summary>
    public static async Task<Result<IReadOnlyList<CatalogPermission>, HttpError>> ResolveCatalogAsync(
        IRepository<Permission, BookingContext> permissions,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return Result.Ok<IReadOnlyList<CatalogPermission>, HttpError>([]);
        }

        var requested = codes.ToHashSet(StringComparer.Ordinal);

        var found = await Result.TryAsync(
            () => permissions
                .QueryDto<CatalogPermission>(q => q.Where(permission => requested.Contains(permission.Code)))
                .ToListAsync(cancellationToken),
            ex => HttpError.Internal(
                "Failed to read the permission catalog.", errorCode: ErrorCodes.Roles.FetchFailed));

        if (found.IsError)
        {
            return found.Error!;
        }

        var unknown = requested
            .Except(found.Data!.Select(permission => permission.Code), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (unknown.Length > 0)
        {
            return HttpError.BadRequest(
                $"No such permission code: {string.Join(", ", unknown)}.",
                errorCode: ErrorCodes.Roles.UnknownPermission);
        }

        return Result.Ok<IReadOnlyList<CatalogPermission>, HttpError>(found.Data!);
    }

    /// <summary>
    /// Applies both rules to a set of codes about to be conferred. The tenant-grantable rule applies
    /// only when <paramref name="atCompanyScope"/> — a platform-provided role is the platform's.
    /// </summary>
    public static Result<HttpError> EnsureCallerMayGrant(
        IReadOnlyCollection<CatalogPermission> permissions,
        bool atCompanyScope,
        ICurrentPermissions caller)
    {
        // First: a platform administrator holds every code by definition, so testing Codes would
        // fail for any permission added after super_admin's role_permissions rows were written.
        if (caller.IsPlatformAdministrator)
        {
            return Result.Ok();
        }

        if (atCompanyScope)
        {
            var forbidden = permissions
                .Where(permission => !permission.IsTenantGrantable)
                .Select(permission => permission.Code)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (forbidden.Length > 0)
            {
                return HttpError.Forbidden(
                    $"These permissions may never be granted within a company: " +
                    $"{string.Join(", ", forbidden)}. Their authority is platform-wide, so confining " +
                    "the grant to one company would not confine what it permits.",
                    errorCode: ErrorCodes.Roles.PermissionNotTenantGrantable);
            }
        }

        var beyondCaller = permissions
            .Select(permission => permission.Code)
            .Where(code => !caller.Has(code))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (beyondCaller.Length > 0)
        {
            return HttpError.Forbidden(
                $"You cannot grant permissions you do not hold here: {string.Join(", ", beyondCaller)}.",
                errorCode: ErrorCodes.Roles.PermissionEscalation);
        }

        return Result.Ok();
    }
}
