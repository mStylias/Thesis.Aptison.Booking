using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

/// <summary>
/// Soft-deletes a member nobody has booked or reviewed, with their services, hours, time off and
/// grants here. A booked one is refused: <c>Appointment.StaffMember</c> is required and filtered, so a
/// deleted member would take whole appointments out of the grid and the calendar.
/// </summary>
public class DeleteStaffMemberRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }
}

public class DeleteStaffMemberHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IPermissionCacheInvalidator cacheInvalidator)
    : ICommandHandler<DeleteStaffMemberRequest, Result<HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<Review, BookingContext> _reviewRepo = unitOfWork.GetRepository<Review>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();
    private readonly IRepository<StaffWorkHour, BookingContext> _workHourRepo =
        unitOfWork.GetRepository<StaffWorkHour>();
    private readonly IRepository<StaffTimeOff, BookingContext> _timeOffRepo = unitOfWork.GetRepository<StaffTimeOff>();
    private readonly IRepository<UserRole, BookingContext> _userRoleRepo = unitOfWork.GetRepository<UserRole>();

    public async Task<Result<HttpError>> HandleAsync(DeleteStaffMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await _staffMemberRepo
            .Query(query => query.Where(row => row.Id == request.StaffMemberId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return StaffErrors.CreateNotFound();
        }

        var mayManage = await StaffRankRules.EnsureMayManageAsync(
            permissionResolver, caller, request.CallerUserId, member.UserId, request.CompanyId,
            refuseSelf: true, cancellationToken);

        if (mayManage.IsError)
        {
            return mayManage.Error!;
        }

        var isReferenced =
            await _appointmentRepo.Query(query => query.Where(row => row.StaffMemberId == member.Id)).AnyAsync(cancellationToken)
            || await _reviewRepo.Query(query => query.Where(row => row.StaffMemberId == member.Id)).AnyAsync(cancellationToken);

        if (isReferenced)
        {
            return HttpError.Conflict(
                "This member is on appointments or reviews and cannot be deleted. Deactivate them instead.",
                errorCode: ErrorCodes.Staff.InUse);
        }

        var assignments = await _staffServiceRepo
            .Query(query => query.Where(row => row.StaffMemberId == member.Id), asNoTracking: false)
            .ToListAsync(cancellationToken);
        var workHours = await _workHourRepo
            .Query(query => query.Where(row => row.StaffMemberId == member.Id), asNoTracking: false)
            .ToListAsync(cancellationToken);
        var timeOff = await _timeOffRepo
            .Query(query => query.Where(row => row.StaffMemberId == member.Id), asNoTracking: false)
            .ToListAsync(cancellationToken);

        // Every grant here, hidden roles included: the membership they belong to is going.
        var grants = await _userRoleRepo
            .Query(query => query.Where(grant => grant.UserId == member.UserId && grant.OwnerId == request.CompanyId),
                asNoTracking: false)
            .ToListAsync(cancellationToken);

        var saved = await Result.TryAsync(
            async () =>
            {
                _staffServiceRepo.RemoveRange(assignments);
                _workHourRepo.RemoveRange(workHours);
                _timeOffRepo.RemoveRange(timeOff);
                _userRoleRepo.RemoveRange(grants);
                _staffMemberRepo.Remove(member);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The staff member could not be deleted. Please try again.",
                errorCode: ErrorCodes.Staff.DeleteFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        await cacheInvalidator.InvalidateUserAsync(member.UserId, cancellationToken);

        return Result.Ok();
    }
}
