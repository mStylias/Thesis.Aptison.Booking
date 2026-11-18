using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Queries;

/// <summary>
/// Upcoming and current time off, soonest first; with <c>Past</c>, what has ended, latest first.
/// Without <c>members:read</c>, only the caller's own active record.
/// </summary>
public class ListStaffTimeOffRequest : IQuery<Result<IReadOnlyList<StaffTimeOffResponse>, HttpError>>
{
    /// <summary>Null when the caller has no company: the endpoint's company scope is optional.</summary>
    public Guid? CompanyId { get; set; }

    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }
    public bool Past { get; set; }
}

public class ListStaffTimeOffHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    ICurrentPermissions caller,
    TimeProvider timeProvider)
    : IQueryHandler<ListStaffTimeOffRequest, Result<IReadOnlyList<StaffTimeOffResponse>, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<StaffTimeOff, BookingContext> _timeOffRepo = unitOfWork.GetRepository<StaffTimeOff>();
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();

    public async Task<Result<IReadOnlyList<StaffTimeOffResponse>, HttpError>> HandleAsync(
        ListStaffTimeOffRequest request, CancellationToken cancellationToken)
    {
        if (request.CompanyId is not { } requestCompanyId)
        {
            return HttpError.Forbidden(
                "This endpoint works on a company, and your account is not on the staff of one.",
                errorCode: ErrorCodes.Authorization.CompanyScopeMissing);
        }

        var companyId = await StaffRowCompanyReader.ReadCompanyIdAsync(
            unitOfWork, userContext, caller, requestCompanyId, request.CallerUserId, request.StaffMemberId,
            cancellationToken);
        using var ownRowScope = companyId != requestCompanyId ? userContext.BecomeOwner(companyId) : null;

        var mayReadAnyMember = caller.IsPlatformAdministrator || caller.Has(PermissionCodes.Members.Read);

        // Without the code, another member's id is a 403 whether it exists or not, so ids cannot be probed.
        var memberExists = await _staffMemberRepo
            .Query(query => query.Where(member =>
                member.Id == request.StaffMemberId
                && (mayReadAnyMember || (member.UserId == request.CallerUserId && member.IsActive))))
            .AnyAsync(cancellationToken);

        if (!memberExists)
        {
            return mayReadAnyMember
                ? StaffErrors.CreateNotFound()
                : HttpError.Forbidden(
                    $"You do not have the '{PermissionCodes.Members.Read}' permission at this company.",
                    errorCode: ErrorCodes.Authorization.PermissionDenied);
        }

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, companyId, cancellationToken);
        var timeZone = BookingCompanyReader.GetTimeZone(company?.Timezone ?? "UTC");
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var rows = await _timeOffRepo
            .QuerySelect(
                timeOff => new { timeOff.Id, timeOff.StartsAt, timeOff.EndsAt, timeOff.Reason },
                query =>
                {
                    var filtered = query.Where(timeOff =>
                        timeOff.StaffMemberId == request.StaffMemberId
                        && (request.Past ? timeOff.EndsAt <= utcNow : timeOff.EndsAt > utcNow));

                    return request.Past
                        ? filtered.OrderByDescending(timeOff => timeOff.StartsAt)
                        : filtered.OrderBy(timeOff => timeOff.StartsAt);
                })
            .ToListAsync(cancellationToken);

        return Result.Ok<IReadOnlyList<StaffTimeOffResponse>, HttpError>(
            [.. rows.Select(row => StaffTimeOffRules.ToView(row.Id, row.StartsAt, row.EndsAt, row.Reason, timeZone))]);
    }
}
