using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Application.Features.Roles.Common.Readers;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Commands;

/// <summary>
/// Grants a role to a member of the company's staff, scoped to that company. Two invariants hold
/// here and only here — any other path that writes <c>user_roles</c> bypasses both silently:
/// <list type="number">
/// <item>The role's permissions must be a subset of what the caller holds in this scope, and none of
/// them non-tenant-grantable. A role is a bag of permissions, so granting one grants all of them.</item>
/// <item>The target must already be on the company's staff.</item>
/// </list>
/// A company-owned role being grantable only at its own company is not restated: <c>roles</c> is
/// ownership-scoped, so another company's role 404s below.
/// </summary>
public class AssignRoleRequest : ICommand<Result<RoleAssignmentResponse, HttpError>>
{
    /// <inheritdoc cref="CreateRoleRequest.CompanyId"/>
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }
}

public class AssignRoleHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentPermissions caller,
    IPermissionCacheInvalidator cacheInvalidator)
    : ICommandHandler<AssignRoleRequest, Result<RoleAssignmentResponse, HttpError>>
{
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<RolePermission, BookingContext> _rolePermissionRepo =
        unitOfWork.GetRepository<RolePermission>();
    private readonly IRepository<UserRole, BookingContext> _userRoleRepo = unitOfWork.GetRepository<UserRole>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();

    public async Task<Result<RoleAssignmentResponse, HttpError>> HandleAsync(
        AssignRoleRequest request, CancellationToken cancellationToken)
    {
        // The ownership filter narrows this to the platform's roles plus the request's company, and
        // RoleVisibility drops the platform-only ones, so "grantable here" and "exists" are the same
        // question. A hidden role is a 404 rather than a 403: a 403 would confirm it exists.
        var roleExists = await _roleRepo
            .Query(q => RoleVisibility.Apply(
                q.Where(candidate => candidate.Id == request.RoleId), caller))
            .AnyAsync(cancellationToken);

        if (!roleExists)
        {
            return HttpError.NotFound(
                "The specified role was not found.", errorCode: ErrorCodes.Roles.NotFound);
        }

        // Invariant 2. staff_members is a required-owner table, so this cannot see another company's.
        var isActiveStaffMember = await _staffMemberRepo
            .Query(q => q.Where(member => member.UserId == request.UserId && member.IsActive))
            .AnyAsync(cancellationToken);

        if (!isActiveStaffMember)
        {
            return HttpError.UnprocessableEntity(
                "That user is not an active member of this company's staff. Add them to the staff " +
                "list before granting them a role here.",
                errorCode: ErrorCodes.RoleAssignments.NotStaffMember);
        }

        // Invariant 1. What the role grants is the database's answer, not the request's.
        var rolePermissions = await _rolePermissionRepo
            .QueryDto<CatalogPermission>(q => q.Where(rolePermission => rolePermission.RoleId == request.RoleId))
            .ToListAsync(cancellationToken);

        var mayGrant = RoleGrantRules.EnsureCallerMayGrant(rolePermissions, atCompanyScope: true, caller);

        if (mayGrant.IsError)
        {
            return mayGrant.Error!;
        }

        var alreadyHeld = await _userRoleRepo
            .Query(q => q.Where(assignment =>
                assignment.UserId == request.UserId
                && assignment.RoleId == request.RoleId
                // Written out: user_roles has an *optional* owner, so the filter would also admit
                // global grants, which are a different fact from a grant at this company.
                && assignment.OwnerId == request.CompanyId))
            .AnyAsync(cancellationToken);

        if (alreadyHeld)
        {
            return HttpError.Conflict(
                "That user already holds this role here.",
                errorCode: ErrorCodes.RoleAssignments.AlreadyAssigned);
        }

        var assignment = new UserRole
        {
            Id = Guid.CreateVersion7(),
            UserId = request.UserId,
            RoleId = request.RoleId,
            OwnerId = request.CompanyId
        };

        var saved = await Result.TryAsync(
            async () =>
            {
                await _userRoleRepo.AddAsync(assignment, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            ex => HttpError.Internal(
                "Failed to assign the role.", errorCode: ErrorCodes.RoleAssignments.AssignFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        // After the commit, so the grant is live on the target's next request.
        await cacheInvalidator.InvalidateUserAsync(request.UserId, cancellationToken);

        var written = await RoleAssignmentReader.ReadAsync(
            unitOfWork, request.CompanyId, userId: request.UserId, caller, cancellationToken);

        if (written.IsError)
        {
            return written.Error!;
        }

        var view = written.Data!.FirstOrDefault(row => row.RoleId == request.RoleId);

        return view is not null
            ? view
            : HttpError.NotFound(
                "The assignment was written but could not be read back.",
                errorCode: ErrorCodes.RoleAssignments.NotFound);
    }
}
