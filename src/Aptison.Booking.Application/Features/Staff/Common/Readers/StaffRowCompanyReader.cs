using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;

namespace Aptison.Booking.Application.Features.Staff.Common.Readers;

/// <summary>
/// A platform administrator can work on another shop through <c>X-Company-Id</c>, but their own staff
/// row stays at their membership's shop. The caller opens <see cref="IUserContextScope.BecomeOwner{TKey}"/>
/// on a different answer itself: a scope opened inside an awaited method ends when it returns.
/// </summary>
internal static class StaffRowCompanyReader
{
    /// <summary>The own row's company when the caller names their own row, else the request's.</summary>
    public static async Task<Guid> ReadCompanyIdAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IUserContextScope userContext,
        ICurrentPermissions caller,
        Guid requestCompanyId,
        Guid callerUserId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        // Anyone else always works at their membership's company.
        if (!caller.IsPlatformAdministrator)
        {
            return requestCompanyId;
        }

        using (userContext.BecomeAllOwners())
        {
            var ownRowCompanyId = await unitOfWork.GetRepository<StaffMember>()
                .QuerySelect(
                    member => (Guid?)member.OwnerId,
                    query => query.Where(member =>
                        member.Id == staffMemberId && member.UserId == callerUserId && member.IsActive))
                .FirstOrDefaultAsync(cancellationToken);

            return ownRowCompanyId ?? requestCompanyId;
        }
    }
}
