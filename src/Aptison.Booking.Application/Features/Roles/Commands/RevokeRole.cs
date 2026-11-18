using Aptison.Booking.Domain;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Commands;

/// <summary>
/// Takes a role away from a user at one company. Refused on the caller's own grants and on a user
/// who outranks the caller, as <see cref="StaffRankRules"/> refuses a re-role. No role subset rule:
/// revocation only removes authority.
/// </summary>
public class RevokeRoleRequest : ICommand<Result<HttpError>>
{
    /// <inheritdoc cref="CreateRoleRequest.CompanyId"/>
    public Guid CompanyId { get; set; }

    public Guid CallerUserId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }
}

public class RevokeRoleHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IPermissionCacheInvalidator cacheInvalidator)
    : ICommandHandler<RevokeRoleRequest, Result<HttpError>>
{
    private readonly IRepository<UserRole, BookingContext> _userRoleRepo = unitOfWork.GetRepository<UserRole>();

    public async Task<Result<HttpError>> HandleAsync(
        RevokeRoleRequest request, CancellationToken cancellationToken)
    {
        // OwnerId compared explicitly, as in AssignRole: user_roles has an optional owner, so the
        // filter alone would also match a global grant of the same role.
        var assignment = await _userRoleRepo
            .Query(
                q => q.Where(candidate =>
                    candidate.UserId == request.UserId
                    && candidate.RoleId == request.RoleId
                    && candidate.OwnerId == request.CompanyId),
                asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            return HttpError.NotFound(
                "That user does not hold this role here.",
                errorCode: ErrorCodes.RoleAssignments.NotFound);
        }

        var mayManage = await StaffRankRules.EnsureMayManageAsync(
            permissionResolver, caller, request.CallerUserId, request.UserId, request.CompanyId,
            refuseSelf: true, cancellationToken);

        if (mayManage.IsError)
        {
            return mayManage.Error!;
        }

        var saved = await Result.TryAsync(
            async () =>
            {
                _userRoleRepo.Remove(assignment);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            ex => HttpError.Internal(
                "Failed to revoke the role.", errorCode: ErrorCodes.RoleAssignments.RevokeFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        // Nothing in the cache expires, so without this the user keeps the permission until Redis
        // is restarted.
        await cacheInvalidator.InvalidateUserAsync(request.UserId, cancellationToken);

        return Result.Ok();
    }
}
