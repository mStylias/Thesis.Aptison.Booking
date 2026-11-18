using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

/// <summary>
/// Takes the member off the shop: they keep the account and their grants, but no company resolves
/// for them. Refused while they have bookings ahead. Past appointments keep showing them.
/// </summary>
public class DeactivateStaffMemberRequest : ICommand<Result<StaffMemberResponse, HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }
}

public class DeactivateStaffMemberHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IPermissionCacheInvalidator cacheInvalidator,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : ICommandHandler<DeactivateStaffMemberRequest, Result<StaffMemberResponse, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();

    public async Task<Result<StaffMemberResponse, HttpError>> HandleAsync(
        DeactivateStaffMemberRequest request, CancellationToken cancellationToken)
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

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var viewer = new StaffViewer(
            request.CompanyId, request.CallerUserId, caller, permissionResolver,
            fileStorageOptions.Value.PublicBaseUrl, utcNow);

        if (!member.IsActive)
        {
            return await StaffMemberReader.ReadAsync(unitOfWork, viewer, member.Id, cancellationToken);
        }

        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        var upcomingCount = await _appointmentRepo
            .Query(query => query.Where(appointment =>
                appointment.StaffMemberId == member.Id
                && activeStatuses.Contains(appointment.Status)
                && appointment.StartsAt > utcNow))
            .CountAsync(cancellationToken);

        if (upcomingCount > 0)
        {
            return HttpErrorWithExtensions.Create(
                HttpError.Conflict(
                    $"This member has {upcomingCount} upcoming appointment(s). Move or cancel them first.",
                    errorCode: ErrorCodes.Staff.HasUpcomingAppointments),
                new Dictionary<string, object?> { ["count"] = upcomingCount });
        }

        var saved = await Result.TryAsync(
            async () =>
            {
                member.IsActive = false;

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The staff member could not be deactivated. Please try again.",
                errorCode: ErrorCodes.Staff.UpdateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        // After the commit: the cached entry still names this company.
        await cacheInvalidator.InvalidateUserAsync(member.UserId, cancellationToken);

        return await StaffMemberReader.ReadAsync(unitOfWork, viewer, member.Id, cancellationToken);
    }
}
