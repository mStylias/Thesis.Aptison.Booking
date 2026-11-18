using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

public class DeleteStaffTimeOffRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }
    public Guid TimeOffId { get; set; }
}

public class DeleteStaffTimeOffHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IUserContextScope userContext,
    ICurrentPermissions caller)
    : ICommandHandler<DeleteStaffTimeOffRequest, Result<HttpError>>
{
    private readonly IRepository<StaffTimeOff, BookingContext> _timeOffRepo = unitOfWork.GetRepository<StaffTimeOff>();

    public async Task<Result<HttpError>> HandleAsync(DeleteStaffTimeOffRequest request, CancellationToken cancellationToken)
    {
        var companyId = await StaffRowCompanyReader.ReadCompanyIdAsync(
            unitOfWork, userContext, caller, request.CompanyId, request.CallerUserId, request.StaffMemberId,
            cancellationToken);
        using var ownRowScope = companyId != request.CompanyId ? userContext.BecomeOwner(companyId) : null;

        var target = await StaffTimeOffTarget.ReadAsync(
            unitOfWork, caller, companyId, request.CallerUserId, request.StaffMemberId, cancellationToken);

        if (target.IsError)
        {
            return target.Error!;
        }

        var timeOff = await _timeOffRepo
            .Query(
                query => query.Where(row => row.Id == request.TimeOffId && row.StaffMemberId == request.StaffMemberId),
                asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (timeOff is null)
        {
            return StaffErrors.CreateTimeOffNotFound();
        }

        var span = StaffTimeOffRules.ToSpan(timeOff.StartsAt, timeOff.EndsAt, target.Data!.TimeZone);

        return await Result.TryAsync(
            async () =>
            {
                _timeOffRepo.Remove(timeOff);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(companyId, span.StartsOn, span.EndsOn), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The time off could not be deleted. Please try again.",
                errorCode: ErrorCodes.Staff.DeleteFailed));
    }
}
