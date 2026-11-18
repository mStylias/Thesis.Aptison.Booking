using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Readers;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Queries;

/// <summary>
/// Every role the caller's scope can see. The ownership filter has already applied the company;
/// <see cref="ListRolesRequest.CompanyId"/> is only for <c>IsEditable</c>.
/// </summary>
public class ListRolesRequest : IQuery<Result<IReadOnlyList<RoleResponse>, HttpError>>
{
    /// <summary>The request's company, never bound from a caller-supplied value.</summary>
    public Guid? CompanyId { get; set; }
}

public class ListRolesHandler(IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller)
    : IQueryHandler<ListRolesRequest, Result<IReadOnlyList<RoleResponse>, HttpError>>
{
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<RolePermission, BookingContext> _rolePermissionRepo =
        unitOfWork.GetRepository<RolePermission>();

    public Task<Result<IReadOnlyList<RoleResponse>, HttpError>> HandleAsync(
        ListRolesRequest request, CancellationToken cancellationToken) =>
        RoleReader.ReadAsync(
            _roleRepo, _rolePermissionRepo, filter: null, request.CompanyId, caller, cancellationToken);
}
