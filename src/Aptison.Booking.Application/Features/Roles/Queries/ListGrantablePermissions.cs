using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Queries;

/// <summary>
/// The permission catalog as this caller sees it at their company — every code, each carrying
/// whether the caller could put it in a role there. Refused codes are flagged rather than omitted,
/// so a role editor can grey them out instead of silently shortening the list.
/// </summary>
public class ListGrantablePermissionsRequest : IQuery<Result<IReadOnlyList<GrantablePermissionResponse>, HttpError>>;

public class ListGrantablePermissionsHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentPermissions caller)
    : IQueryHandler<ListGrantablePermissionsRequest, Result<IReadOnlyList<GrantablePermissionResponse>, HttpError>>
{
    private readonly IRepository<Permission, BookingContext> _permissionRepo =
        unitOfWork.GetRepository<Permission>();

    public async Task<Result<IReadOnlyList<GrantablePermissionResponse>, HttpError>> HandleAsync(
        ListGrantablePermissionsRequest request, CancellationToken cancellationToken)
    {
        return await Result.TryAsync(
                () => _permissionRepo
                    .QueryDto<PermissionCatalogDto>(
                        q => q.OrderBy(permission => permission.Category).ThenBy(permission => permission.Code))
                    .ToListAsync(cancellationToken),
                ex => HttpError.Internal(
                    "Failed to read the permission catalog.", errorCode: ErrorCodes.Roles.FetchFailed))
            .MapAsync(rows => (IReadOnlyList<GrantablePermissionResponse>)
            [
                .. rows.Select(row => new GrantablePermissionResponse(
                    row.Code,
                    row.Name,
                    row.Description,
                    row.Category,
                    row.IsTenantGrantable,
                    // A preview of RoleGrantRules, never the enforcement.
                    CanGrant: caller.IsPlatformAdministrator
                              || (row.IsTenantGrantable && caller.Has(row.Code)))),
            ]);
    }
}
