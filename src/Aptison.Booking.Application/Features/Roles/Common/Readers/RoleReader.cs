using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Common.Readers;

/// <summary>
/// Reads roles with their permission codes attached; shared by the list, the detail and both write
/// endpoints.
/// <para>
/// Two queries, not a navigation: projecting through one would reach <c>role_permissions</c> as a
/// LEFT JOIN under the soft-delete filters, the trap <c>PermissionStore</c> documents. Neither
/// query names a company — <c>roles</c> is ownership-scoped, so the filter has already narrowed
/// the set, and a hand-written predicate would be a weaker second copy of that rule.
/// </para>
/// </summary>
internal static class RoleReader
{
    public static async Task<Result<IReadOnlyList<RoleResponse>, HttpError>> ReadAsync(
        IRepository<Role, BookingContext> roles,
        IRepository<RolePermission, BookingContext> rolePermissions,
        Func<IQueryable<Role>, IQueryable<Role>>? filter,
        Guid? companyId,
        ICurrentPermissions caller,
        CancellationToken cancellationToken)
    {
        var roleRows = await Result.TryAsync(
            () => roles
                .QueryDto<RoleSummaryDto>(
                    q => RoleVisibility.Apply(filter is null ? q : filter(q), caller)
                        // Platform roles first, then the company's own. Ordering the entity query
                        // rather than the projection is what keeps it in SQL.
                        .OrderBy(role => role.OwnerId != null)
                        .ThenBy(role => role.Name))
                .ToListAsync(cancellationToken),
            ex => HttpError.Internal("Failed to fetch roles.", errorCode: ErrorCodes.Roles.FetchFailed));

        if (roleRows.IsError)
        {
            return roleRows.Error!;
        }

        if (roleRows.Data!.Count == 0)
        {
            return Result.Ok<IReadOnlyList<RoleResponse>, HttpError>([]);
        }

        var roleIds = roleRows.Data!.Select(row => row.Id).ToArray();

        var rolePermissionRows = await Result.TryAsync(
            () => rolePermissions
                .QuerySelect(
                    rolePermission => new { rolePermission.RoleId, rolePermission.Permission.Code },
                    q => q.Where(rolePermission => roleIds.Contains(rolePermission.RoleId)))
                .ToListAsync(cancellationToken),
            ex => HttpError.Internal(
                "Failed to fetch role permissions.", errorCode: ErrorCodes.Roles.FetchFailed));

        if (rolePermissionRows.IsError)
        {
            return rolePermissionRows.Error!;
        }

        var codesByRole = rolePermissionRows.Data!
            .GroupBy(pair => pair.RoleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(pair => pair.Code).Order(StringComparer.Ordinal)]);

        var roleViews = roleRows.Data!
            .Select(row => new RoleResponse(
                row.Id,
                row.OwnerId,
                row.Name,
                row.Description,
                row.IsSystem,
                // "This scope administers the definition" — a system role is writable nowhere.
                IsEditable: row.OwnerId is not null && row.OwnerId == companyId && !row.IsSystem,
                codesByRole.TryGetValue(row.Id, out var permissions) ? permissions : []))
            .ToArray();

        return Result.Ok<IReadOnlyList<RoleResponse>, HttpError>(roleViews);
    }
}
