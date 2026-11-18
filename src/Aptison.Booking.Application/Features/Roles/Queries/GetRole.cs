using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Readers;
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
/// One role, with the permissions it grants. Another company's role is a 404, not a 403: the
/// ownership filter removes the row before this handler sees it.
/// </summary>
public class GetRoleRequest : IQuery<Result<RoleResponse, HttpError>>
{
    public Guid RoleId { get; set; }

    /// <summary>The request's company, or <c>null</c> at platform scope.</summary>
    public Guid? CompanyId { get; set; }
}

public class GetRoleHandler(IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller)
    : IQueryHandler<GetRoleRequest, Result<RoleResponse, HttpError>>
{
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<RolePermission, BookingContext> _rolePermissionRepo =
        unitOfWork.GetRepository<RolePermission>();

    public async Task<Result<RoleResponse, HttpError>> HandleAsync(
        GetRoleRequest request, CancellationToken cancellationToken)
    {
        var roles = await RoleReader.ReadAsync(
            _roleRepo,
            _rolePermissionRepo,
            q => q.Where(role => role.Id == request.RoleId),
            request.CompanyId,
            caller,
            cancellationToken);

        if (roles.IsError)
        {
            return roles.Error!;
        }

        return roles.Data!.Count > 0
            ? roles.Data![0]
            : HttpError.NotFound("The specified role was not found.", errorCode: ErrorCodes.Roles.NotFound);
    }
}
