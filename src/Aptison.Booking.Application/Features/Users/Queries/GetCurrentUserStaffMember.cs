using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Users.Queries;

/// <summary>The caller's active membership, with the week and the shop hours the profile page edits it against.</summary>
public class GetCurrentUserStaffMemberRequest : IQuery<Result<CurrentStaffMemberResponse, HttpError>>
{
    /// <summary>From the token in the endpoint.</summary>
    public Guid UserId { get; set; }
}

public class GetCurrentUserStaffMemberHandler(IUnitOfWork<BookingContext> unitOfWork, IUserContextScope userContext)
    : IQueryHandler<GetCurrentUserStaffMemberRequest, Result<CurrentStaffMemberResponse, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();

    public async Task<Result<CurrentStaffMemberResponse, HttpError>> HandleAsync(
        GetCurrentUserStaffMemberRequest request, CancellationToken cancellationToken)
    {
        // The membership decides the company, so no request scope may narrow or replace it.
        using (userContext.BecomeAllOwners())
        {
            var member = await _staffMemberRepo
                .QuerySelect(
                    member => new
                    {
                        member.Id,
                        member.OwnerId,
                        member.User.FirstName,
                        member.User.LastName,
                        member.JobTitleCode,
                        member.IsBookable,
                        member.Owner.Timezone,
                        member.Owner.SlotIntervalMinutes,
                        WorkHours = member.WorkHours
                            .Select(hour => new { hour.DayOfWeek, hour.StartTime, hour.EndTime })
                            .ToList(),
                        BusinessHours = member.Owner.BusinessHours
                            .Select(hour => new { hour.DayOfWeek, hour.OpenTime, hour.CloseTime })
                            .ToList()
                    },
                    query => query.Where(member => member.UserId == request.UserId && member.IsActive))
                .FirstOrDefaultAsync(cancellationToken);

            if (member is null)
            {
                return HttpError.NotFound(
                    "You are not on the staff of a shop.", errorCode: ErrorCodes.Users.NotStaffMember);
            }

            return new CurrentStaffMemberResponse(
                member.Id,
                member.OwnerId,
                StaffMemberNames.GetDisplayName(member.FirstName, member.LastName),
                member.JobTitleCode,
                member.IsBookable,
                StaffWorkHourRules.ToViews(member.WorkHours.Select(hour => (hour.DayOfWeek, hour.StartTime, hour.EndTime))),
                member.Timezone,
                member.SlotIntervalMinutes,
                BusinessHourRules.ToViews(member.BusinessHours.Select(hour => (hour.DayOfWeek, hour.OpenTime, hour.CloseTime))));
        }
    }
}
