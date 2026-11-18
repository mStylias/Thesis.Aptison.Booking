using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Application.Features.Staff.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

/// <summary>The member falls back to initials. A member without a photo is a no-op.</summary>
public class DeleteStaffPhotoRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }
}

public class DeleteStaffPhotoHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver)
    : ICommandHandler<DeleteStaffPhotoRequest, Result<HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();

    public async Task<Result<HttpError>> HandleAsync(DeleteStaffPhotoRequest request, CancellationToken cancellationToken)
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
            refuseSelf: false, cancellationToken);

        if (mayManage.IsError)
        {
            return mayManage.Error!;
        }

        if (member.AvatarStorageKey is not { } oldStorageKey)
        {
            return Result.Ok();
        }

        return await Result.TryAsync(
            async () =>
            {
                member.AvatarStorageKey = null;

                await outbox.EnqueueAsync(new StaffPhotoReplaced(oldStorageKey), cancellationToken);
                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The photo could not be removed. Please try again.",
                errorCode: ErrorCodes.Staff.UpdateFailed));
    }
}
