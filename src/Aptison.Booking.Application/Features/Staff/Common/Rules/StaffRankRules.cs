using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Common.Rules;

/// <summary>
/// The member-side twin of <see cref="Roles.Common.Rules.RoleGrantRules"/>: nobody activates,
/// deactivates, deletes, re-roles or edits a member holding a permission here they do not hold, or
/// revokes a role from them. The platform administrator passes.
/// </summary>
internal static class StaffRankRules
{
    public static async Task<bool> IsOutrankedAsync(
        IPermissionResolver permissionResolver,
        ICurrentPermissions caller,
        Guid memberUserId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (caller.IsPlatformAdministrator)
        {
            return false;
        }

        var member = await permissionResolver.ResolveAsync(memberUserId, companyId, cancellationToken);

        return member.IsPlatformAdministrator || member.Codes.Any(code => !caller.Has(code));
    }

    /// <param name="refuseSelf">For a change to a membership or its roles: a caller could lock themselves out.</param>
    public static async Task<Result<HttpError>> EnsureMayManageAsync(
        IPermissionResolver permissionResolver,
        ICurrentPermissions caller,
        Guid callerUserId,
        Guid memberUserId,
        Guid companyId,
        bool refuseSelf,
        CancellationToken cancellationToken)
    {
        if (caller.IsPlatformAdministrator)
        {
            return Result.Ok();
        }

        if (refuseSelf && callerUserId == memberUserId)
        {
            return HttpError.Conflict(
                "You cannot deactivate, delete or change the roles of your own membership.",
                errorCode: ErrorCodes.Staff.CannotChangeSelf);
        }

        if (await IsOutrankedAsync(permissionResolver, caller, memberUserId, companyId, cancellationToken))
        {
            return HttpError.Forbidden(
                "This member holds permissions here that you do not, so you cannot change them.",
                errorCode: ErrorCodes.Staff.OutranksCaller);
        }

        return Result.Ok();
    }

    /// <summary>The wide code reaches any member; the own code only the caller's own row.</summary>
    public static Result<HttpError> EnsureMayWriteSchedule(
        ICurrentPermissions caller, string anyMemberCode, string ownRowCode, Guid callerUserId, Guid memberUserId)
    {
        if (caller.IsPlatformAdministrator
            || caller.Has(anyMemberCode)
            || (caller.Has(ownRowCode) && callerUserId == memberUserId))
        {
            return Result.Ok();
        }

        return HttpError.Forbidden(
            "You may change only your own hours and time off.", errorCode: ErrorCodes.Staff.NotOwnRow);
    }
}
