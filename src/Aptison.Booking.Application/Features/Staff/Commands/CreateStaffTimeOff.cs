using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
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

/// <summary>Upcoming bookings inside the time off are reported, not moved.</summary>
public class CreateStaffTimeOffRequest : StaffTimeOffWriteRequest, ICommand<Result<StaffScheduleChangeResponse, HttpError>>;

public class CreateStaffTimeOffHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IUserContextScope userContext,
    ICurrentPermissions caller,
    IDatabaseConstraintReader constraintReader,
    TimeProvider timeProvider)
    : ICommandHandler<CreateStaffTimeOffRequest, Result<StaffScheduleChangeResponse, HttpError>>
{
    private readonly IRepository<StaffTimeOff, BookingContext> _timeOffRepo = unitOfWork.GetRepository<StaffTimeOff>();

    public async Task<Result<StaffScheduleChangeResponse, HttpError>> HandleAsync(
        CreateStaffTimeOffRequest request, CancellationToken cancellationToken)
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

        var timeZone = target.Data!.TimeZone;
        var input = StaffTimeOffRules.Parse(request, timeZone);

        if (input.IsError)
        {
            return input.Error!;
        }

        var (startsAt, endsAt, reason, startsOn, endsOn) = input.Data!;

        if (await target.Data.OverlapsAsync(_timeOffRepo, startsAt, endsAt, excludingTimeOffId: null, cancellationToken))
        {
            return StaffTimeOffTarget.CreateOverlapError();
        }

        var timeOff = new StaffTimeOff
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            StaffMemberId = request.StaffMemberId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Reason = reason
        };

        try
        {
            await _timeOffRepo.AddAsync(timeOff, cancellationToken);
            await outbox.EnqueueAsync(new CompanyCalendarChanged(companyId, startsOn, endsOn), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, StaffConstraints.TimeOffNoOverlap))
        {
            return StaffTimeOffTarget.CreateOverlapError();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The time off could not be saved. Please try again.", errorCode: ErrorCodes.Staff.UpdateFailed);
        }

        var conflicts = await StaffConflictReader.ReadInsideTimeOffAsync(
            unitOfWork, request.StaffMemberId, startsAt, endsAt, timeZone, timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return new StaffScheduleChangeResponse(
            null, StaffTimeOffRules.ToView(timeOff.Id, startsAt, endsAt, reason, timeZone), conflicts);
    }
}
