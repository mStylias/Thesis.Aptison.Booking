using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Models;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Common.Rules;

/// <summary>
/// A member's roles at the company, as the staff page writes them. Only the roles the caller can
/// see are compared, so a grant hidden from them is neither shown nor revoked.
/// </summary>
internal static class StaffRoleRules
{
    /// <summary>
    /// Every role must be visible here; the ones not already held go through
    /// <see cref="RoleGrantRules"/>, the same rules as <c>POST /roles/assignments</c>.
    /// </summary>
    public static async Task<Result<List<Guid>, HttpError>> CheckAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        ICurrentPermissions caller,
        IReadOnlyList<Guid> roleIds,
        IReadOnlyCollection<Guid> heldRoleIds,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var requestedRoleIds = roleIds.Distinct().ToList();

        var visibleRoleIds = await unitOfWork.GetRepository<Role>()
            .QuerySelect(
                role => role.Id,
                query => RoleVisibility.Apply(query.Where(role => requestedRoleIds.Contains(role.Id)), caller))
            .ToHashSetAsync(cancellationToken);

        var validation = new Validation("The roles are invalid.");

        for (var index = 0; index < roleIds.Count; index++)
        {
            validation.Require(visibleRoleIds.Contains(roleIds[index]), ErrorCodes.Staff.UnknownRole,
                "The role was not found.", $"{propertyName}[{index}]");
        }

        if (validation.HasErrors)
        {
            return validation.Error;
        }

        var addedRoleIds = requestedRoleIds.Where(roleId => !heldRoleIds.Contains(roleId)).ToList();

        var addedPermissions = await unitOfWork.GetRepository<RolePermission>()
            .QueryDto<CatalogPermission>(query => query.Where(rolePermission => addedRoleIds.Contains(rolePermission.RoleId)))
            .ToListAsync(cancellationToken);

        var mayGrant = RoleGrantRules.EnsureCallerMayGrant(addedPermissions, atCompanyScope: true, caller);

        if (mayGrant.IsError)
        {
            return mayGrant.Error!;
        }

        return requestedRoleIds;
    }

    /// <summary>The member's grants at the company whose role the caller can see, tracked.</summary>
    public static async Task<List<UserRole>> ReadVisibleGrantsAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        ICurrentPermissions caller,
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        // OwnerId written out: user_roles has an optional owner, so the filter also admits global grants.
        var grants = await unitOfWork.GetRepository<UserRole>()
            .Query(
                query => query.Where(grant => grant.UserId == userId && grant.OwnerId == companyId),
                asNoTracking: false)
            .ToListAsync(cancellationToken);

        var grantedRoleIds = grants.Select(grant => grant.RoleId).Distinct().ToList();

        var visibleRoleIds = await unitOfWork.GetRepository<Role>()
            .QuerySelect(
                role => role.Id,
                query => RoleVisibility.Apply(query.Where(role => grantedRoleIds.Contains(role.Id)), caller))
            .ToHashSetAsync(cancellationToken);

        return [.. grants.Where(grant => visibleRoleIds.Contains(grant.RoleId))];
    }

    /// <summary>Revokes the grants not wanted and adds the missing ones. Stages only; the caller saves.</summary>
    public static async Task ReplaceGrantsAsync(
        IRepository<UserRole, BookingContext> userRoleRepo,
        Guid userId,
        Guid companyId,
        IReadOnlyList<UserRole> currentGrants,
        IReadOnlyCollection<Guid> wantedRoleIds,
        CancellationToken cancellationToken)
    {
        foreach (var grant in currentGrants.Where(grant => !wantedRoleIds.Contains(grant.RoleId)))
        {
            userRoleRepo.Remove(grant);
        }

        var heldRoleIds = currentGrants.Select(grant => grant.RoleId).ToHashSet();

        foreach (var roleId in wantedRoleIds.Where(roleId => !heldRoleIds.Contains(roleId)))
        {
            await userRoleRepo.AddAsync(
                new UserRole { Id = Guid.CreateVersion7(), UserId = userId, RoleId = roleId, OwnerId = companyId },
                cancellationToken);
        }
    }
}
