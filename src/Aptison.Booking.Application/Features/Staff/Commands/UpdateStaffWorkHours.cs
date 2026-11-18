using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
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
using Synax.EntityFrameworkCore.Mediator;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

/// <summary>
/// A full replace of the member's week. Hours outside the shop's are kept; they simply give no
/// slots. Upcoming bookings left outside the new hours are reported, not moved.
/// </summary>
public class UpdateStaffWorkHoursRequest : ICommand<Result<StaffScheduleChangeResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    [JsonIgnore]
    public Guid StaffMemberId { get; set; }

    public IReadOnlyList<StaffWorkHourAddEditDto> WorkHours { get; set; } = [];
}

/// <remarks>
/// Transactional: the old week is soft-deleted in SQL before the new one is inserted, because
/// <c>staff_work_hours_no_overlap</c> checks each row as it lands and EF does not order an update
/// before an insert.
/// </remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class UpdateStaffWorkHoursHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IUserContextScope userContext,
    ICurrentPermissions caller,
    IDatabaseConstraintReader constraintReader,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateStaffWorkHoursRequest, Result<StaffScheduleChangeResponse, HttpError>>,
        IWithValidation<UpdateStaffWorkHoursRequest, Result<List<StaffWorkHourInput>, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<StaffWorkHour, BookingContext> _workHourRepo =
        unitOfWork.GetRepository<StaffWorkHour>();

    public Task<Result<List<StaffWorkHourInput>, HttpError>> ValidateAsync(
        UpdateStaffWorkHoursRequest request, CancellationToken cancellationToken)
    {
        var validation = StaffWorkHourRules.Check(
            new Validation("The work hours are invalid."), request.WorkHours ?? [], nameof(request.WorkHours),
            out var workHours);

        return Task.FromResult(validation.ToResult(workHours));
    }

    public async Task<Result<StaffScheduleChangeResponse, HttpError>> HandleAsync(
        UpdateStaffWorkHoursRequest request, CancellationToken cancellationToken)
    {
        var workHours = this.ValidationData();

        var companyId = await StaffRowCompanyReader.ReadCompanyIdAsync(
            unitOfWork, userContext, caller, request.CompanyId, request.CallerUserId, request.StaffMemberId,
            cancellationToken);
        using var ownRowScope = companyId != request.CompanyId ? userContext.BecomeOwner(companyId) : null;

        var memberUserId = await _staffMemberRepo
            .QuerySelect(member => (Guid?)member.UserId, query => query.Where(member => member.Id == request.StaffMemberId))
            .FirstOrDefaultAsync(cancellationToken);

        if (memberUserId is null)
        {
            return StaffErrors.CreateNotFound();
        }

        var mayWrite = StaffRankRules.EnsureMayWriteSchedule(
            caller, PermissionCodes.Staff.ManageSchedule, PermissionCodes.Staff.ManageOwnSchedule,
            request.CallerUserId, memberUserId.Value);

        if (mayWrite.IsError)
        {
            return mayWrite.Error!;
        }

        try
        {
            await _workHourRepo.ExecuteDeleteAsync(
                query => query.Where(hour => hour.StaffMemberId == request.StaffMemberId), cancellationToken);

            foreach (var hour in workHours)
            {
                await _workHourRepo.AddAsync(
                    StaffWorkHourRules.CreateRow(companyId, request.StaffMemberId, hour), cancellationToken);
            }

            await outbox.EnqueueAsync(new CompanyCalendarChanged(companyId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, StaffConstraints.WorkHoursNoOverlap))
        {
            return HttpError.Conflict(
                "The hours were changed by someone else at the same time. Please reload and try again.",
                errorCode: ErrorCodes.Staff.WorkHoursOverlap);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The work hours could not be saved. Please try again.", errorCode: ErrorCodes.Staff.UpdateFailed);
        }

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), companyId, cancellationToken);

        var conflicts = await StaffConflictReader.ReadOutsideHoursAsync(
            unitOfWork, request.StaffMemberId, workHours,
            BookingCompanyReader.GetTimeZone(company?.Timezone ?? "UTC"),
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        return new StaffScheduleChangeResponse(
            StaffWorkHourRules.ToViews(workHours.Select(hour => (hour.DayOfWeek, hour.Start, hour.End))),
            null,
            conflicts);
    }
}
