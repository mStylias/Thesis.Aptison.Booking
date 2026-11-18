using System.Text.Json.Serialization;
using Aptison.Booking.Domain;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Commands;

/// <summary>
/// Soft-deletes a company-owned role and revokes it from everyone holding it. The one write whose
/// blast radius is more than a single cache entry, so the assignments go with the role and every
/// affected user's cached grant list is dropped by name.
/// </summary>
public class DeleteRoleRequest : ICommand<Result<HttpError>>
{
    /// <inheritdoc cref="CreateRoleRequest.CompanyId"/>
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid RoleId { get; set; }
}

public class DeleteRoleHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IPermissionCacheInvalidator cacheInvalidator)
    : ICommandHandler<DeleteRoleRequest, Result<HttpError>>
{
    // Above this many holders, flush everything instead of enumerating.
    private const int FlushEverythingAbove = 100;

    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<UserRole, BookingContext> _userRoleRepo = unitOfWork.GetRepository<UserRole>();

    public async Task<Result<HttpError>> HandleAsync(
        DeleteRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleRepo
            .Query(q => q.Where(candidate => candidate.Id == request.RoleId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        var writable = UpdateRoleHandler.EnsureWritable(role, request.CompanyId);

        if (writable.IsError)
        {
            return writable.Error!;
        }

        // A company-owned role is only ever granted at its own company, so the ownership filter
        // cannot hide a grant from this query.
        var grants = await _userRoleRepo
            .Query(q => q.Where(assignment => assignment.RoleId == request.RoleId), asNoTracking: false)
            .ToListAsync(cancellationToken);

        var affectedUsers = grants.Select(assignment => assignment.UserId).Distinct().ToArray();

        var saved = await Result.TryAsync(
            async () =>
            {
                // role_permissions is left alone: it describes what the role was, and the role row
                // it hangs off is the thing that is gone.
                foreach (var assignment in grants)
                {
                    _userRoleRepo.Remove(assignment);
                }

                _roleRepo.Remove(role!);

                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            ex => HttpError.Internal("Failed to delete the role.", errorCode: ErrorCodes.Roles.DeleteFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        if (affectedUsers.Length > FlushEverythingAbove)
        {
            await cacheInvalidator.InvalidateAllAsync(cancellationToken);
            return Result.Ok();
        }

        await cacheInvalidator.InvalidateRoleAsync(request.RoleId, cancellationToken);

        // Dropping the role from the map is not enough: their cached grant list still names the
        // role id, so the resolver would rebuild the whole map on every request of theirs, forever.
        foreach (var userId in affectedUsers)
        {
            await cacheInvalidator.InvalidateUserAsync(userId, cancellationToken);
        }

        return Result.Ok();
    }
}
