using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Common.Readers;

/// <summary>
/// Finds the person an owner adds by email. The lookup and the add both run it, so the answer
/// cannot change between them without the add noticing.
/// </summary>
internal static class StaffCandidateReader
{
    public static async Task<Result<StaffCandidate, HttpError>> FindAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IUserContextScope userContext,
        Guid companyId,
        string? email,
        string propertyName,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var address = StringUtils.TrimToNull(email);

        if (address is null)
        {
            return HttpError.BadRequest("An email address is required.", errorCode: ErrorCodes.Staff.InvalidEmail)
                .WithProperty(propertyName);
        }

        // Equality on a citext column: the whole address, any case. Never a pattern, so the lookup
        // cannot be used to list who has an account.
        var candidate = await unitOfWork.GetRepository<User>()
            .QueryDto<StaffCandidate>(
                query => query.Where(user =>
                    user.Email == address
                    && user.KeycloakId != null
                    && (!user.IsBanned || (user.BannedUntil != null && user.BannedUntil <= utcNow))))
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return HttpError.NotFound(
                    "No account uses this address. The person must sign up first.",
                    errorCode: ErrorCodes.Staff.CandidateNotFound)
                .WithProperty(propertyName);
        }

        var memberHere = await unitOfWork.GetRepository<StaffMember>()
            .QuerySelect(member => new { member.Id, member.IsActive }, query => query.Where(member => member.UserId == candidate.UserId))
            .FirstOrDefaultAsync(cancellationToken);

        if (memberHere is not null)
        {
            return HttpErrorWithExtensions.Create(
                HttpError.Conflict("This person is already on the staff.", errorCode: ErrorCodes.Staff.AlreadyMember)
                    .WithProperty(propertyName),
                new Dictionary<string, object?>
                {
                    ["staffMemberId"] = memberHere.Id,
                    ["isActive"] = memberHere.IsActive
                });
        }

        if (await IsActiveElsewhereAsync(unitOfWork, userContext, candidate.UserId, companyId, cancellationToken))
        {
            return CreateEmployedElsewhereError().WithProperty(propertyName);
        }

        return candidate;
    }

    /// <summary>Another shop's active row, which <c>ux_staff_members_user</c> would refuse beside a second one.</summary>
    public static async Task<bool> IsActiveElsewhereAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IUserContextScope userContext,
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        using var _ = userContext.BecomeAllOwners();

        return await unitOfWork.GetRepository<StaffMember>()
            .Query(query => query.Where(member => member.UserId == userId && member.IsActive && member.OwnerId != companyId))
            .AnyAsync(cancellationToken);
    }

    /// <summary>Never names the other shop.</summary>
    public static HttpError CreateEmployedElsewhereError() =>
        HttpError.Conflict("This person works at another shop.", errorCode: ErrorCodes.Staff.EmployedElsewhere);
}
