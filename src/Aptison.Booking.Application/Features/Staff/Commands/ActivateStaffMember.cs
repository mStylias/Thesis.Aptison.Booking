using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

/// <summary>Brings a deactivated member back, with the grants they kept. Refused while they work at another shop.</summary>
public class ActivateStaffMemberRequest : ICommand<Result<StaffMemberResponse, HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }
}

public class ActivateStaffMemberHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IUserContextScope userContext,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IPermissionCacheInvalidator cacheInvalidator,
    IDatabaseConstraintReader constraintReader,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : ICommandHandler<ActivateStaffMemberRequest, Result<StaffMemberResponse, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();

    public async Task<Result<StaffMemberResponse, HttpError>> HandleAsync(
        ActivateStaffMemberRequest request, CancellationToken cancellationToken)
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

        var viewer = new StaffViewer(
            request.CompanyId, request.CallerUserId, caller, permissionResolver,
            fileStorageOptions.Value.PublicBaseUrl, timeProvider.GetUtcNow().UtcDateTime);

        if (member.IsActive)
        {
            return await StaffMemberReader.ReadAsync(unitOfWork, viewer, member.Id, cancellationToken);
        }

        if (await StaffCandidateReader.IsActiveElsewhereAsync(
                unitOfWork, userContext, member.UserId, request.CompanyId, cancellationToken))
        {
            return StaffCandidateReader.CreateEmployedElsewhereError();
        }

        try
        {
            member.IsActive = true;

            await outbox.EnqueueAsync(
                new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, StaffConstraints.OneActiveEmploymentPerUser))
        {
            return StaffCandidateReader.CreateEmployedElsewhereError();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The staff member could not be activated. Please try again.", errorCode: ErrorCodes.Staff.UpdateFailed);
        }

        await cacheInvalidator.InvalidateUserAsync(member.UserId, cancellationToken);

        return await StaffMemberReader.ReadAsync(unitOfWork, viewer, member.Id, cancellationToken);
    }
}
