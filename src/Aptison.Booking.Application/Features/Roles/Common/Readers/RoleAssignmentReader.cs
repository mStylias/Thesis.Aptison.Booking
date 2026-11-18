using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Application.Features.Users.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Common.Readers;

/// <summary>
/// Reads who holds what at one company. Three queries, not navigations: <c>assignment.Role</c>
/// would reach a soft-delete-filtered entity as a LEFT JOIN, so a deleted role would come back as
/// a null name rather than an absent row.
/// </summary>
internal static class RoleAssignmentReader
{
    public static async Task<Result<IReadOnlyList<RoleAssignmentResponse>, HttpError>> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        Guid? userId,
        ICurrentPermissions caller,
        CancellationToken cancellationToken)
    {
        var userRoleRepo = unitOfWork.GetRepository<UserRole>();
        var roleRepo = unitOfWork.GetRepository<Role>();
        var userRepo = unitOfWork.GetRepository<User>();

        var assignmentRows = await Result.TryAsync(
            () => userRoleRepo
                .QuerySelect(
                    assignment => new
                    {
                        assignment.UserId,
                        assignment.RoleId,
                        GrantedAt = assignment.CreatedAt
                    },
                    q => q.Where(assignment =>
                        // Written out: user_roles has an optional owner, so the filter admits
                        // global grants and would expose platform-wide super_admin holders.
                        assignment.OwnerId == companyId
                        && (userId == null || assignment.UserId == userId)))
                .ToListAsync(cancellationToken),
            ex => HttpError.Internal(
                "Failed to fetch role assignments.", errorCode: ErrorCodes.RoleAssignments.FetchFailed));

        if (assignmentRows.IsError)
        {
            return assignmentRows.Error!;
        }

        if (assignmentRows.Data!.Count == 0)
        {
            return Result.Ok<IReadOnlyList<RoleAssignmentResponse>, HttpError>([]);
        }

        var roleIds = assignmentRows.Data!.Select(row => row.RoleId).Distinct().ToArray();
        var userIds = assignmentRows.Data!.Select(row => row.UserId).Distinct().ToArray();

        var roleNamesAndUsers = await Result.TryAsync(
            async () =>
            {
                // Visibility-filtered, so a grant of a hidden role drops out of the list below
                // rather than naming it.
                var roleNamesById = await roleRepo
                    .QuerySelect(
                        role => new { role.Id, role.Name },
                        q => RoleVisibility.Apply(q.Where(role => roleIds.Contains(role.Id)), caller))
                    .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

                // Grantees own their rows, so the ownership filter hides none; the assignment list
                // it looks up was already narrowed to this company.
                var usersById = await userRepo
                    .QueryDto<UserSummaryViewDto>(q => q.Where(user => userIds.Contains(user.Id)))
                    .ToDictionaryAsync(person => person.Id, cancellationToken);

                return (roleNamesById, usersById);
            },
            ex => HttpError.Internal(
                "Failed to fetch role assignments.", errorCode: ErrorCodes.RoleAssignments.FetchFailed));

        if (roleNamesAndUsers.IsError)
        {
            return roleNamesAndUsers.Error!;
        }

        var (roleNamesById, usersById) = roleNamesAndUsers.Data;

        var assignmentViews = assignmentRows.Data!
            .Where(row => roleNamesById.ContainsKey(row.RoleId) && usersById.ContainsKey(row.UserId))
            .Select(row => new RoleAssignmentResponse(
                row.UserId,
                usersById[row.UserId].FirstName,
                usersById[row.UserId].LastName,
                usersById[row.UserId].Email,
                row.RoleId,
                roleNamesById[row.RoleId],
                companyId,
                row.GrantedAt))
            .OrderBy(view => view.LastName)
            .ThenBy(view => view.FirstName)
            .ThenBy(view => view.RoleName)
            .ToArray();

        return Result.Ok<IReadOnlyList<RoleAssignmentResponse>, HttpError>(assignmentViews);
    }
}
