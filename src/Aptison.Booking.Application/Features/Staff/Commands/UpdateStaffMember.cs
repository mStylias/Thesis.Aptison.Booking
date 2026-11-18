using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
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
/// The member's profile and roles at this company. The person's own account — name, email, phone —
/// is theirs and is never written here. Changing roles also needs <c>roles:assign</c>.
/// </summary>
public class UpdateStaffMemberRequest : ICommand<Result<StaffMemberResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    [JsonIgnore]
    public Guid StaffMemberId { get; set; }

    public string? JobTitleCode { get; set; }
    public bool IsBookable { get; set; }

    /// <summary>Shop-local <c>yyyy-MM-dd</c>.</summary>
    public string? JoinedAt { get; set; }

    /// <summary>A full replace of the roles the caller can see. Empty is allowed.</summary>
    public IReadOnlyList<Guid> RoleIds { get; set; } = [];
}

/// <remarks>No transaction behavior: a role change evicts the permission cache after the commit.</remarks>
public class UpdateStaffMemberHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IPermissionCacheInvalidator cacheInvalidator,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateStaffMemberRequest, Result<StaffMemberResponse, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<UserRole, BookingContext> _userRoleRepo = unitOfWork.GetRepository<UserRole>();

    public async Task<Result<StaffMemberResponse, HttpError>> HandleAsync(
        UpdateStaffMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await _staffMemberRepo
            .Query(query => query.Where(row => row.Id == request.StaffMemberId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return StaffErrors.CreateNotFound();
        }

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), request.CompanyId, cancellationToken);
        var timeZone = BookingCompanyReader.GetTimeZone(company?.Timezone ?? "UTC");

        var validation = new Validation("The staff member is invalid.");
        var jobTitleCode = StaffProfileRules.CheckJobTitle(validation, request.JobTitleCode, nameof(request.JobTitleCode));
        var joinedOn = StaffProfileRules.CheckJoinedAt(validation, request.JoinedAt, nameof(request.JoinedAt));

        if (validation.HasErrors)
        {
            return validation.Error;
        }

        var currentGrants = await StaffRoleRules.ReadVisibleGrantsAsync(
            unitOfWork, caller, member.UserId, request.CompanyId, cancellationToken);
        var heldRoleIds = currentGrants.Select(grant => grant.RoleId).ToHashSet();
        var wantedRoleIds = (request.RoleIds ?? []).ToHashSet();
        var rolesChange = !heldRoleIds.SetEquals(wantedRoleIds);

        var mayManage = await StaffRankRules.EnsureMayManageAsync(
            permissionResolver, caller, request.CallerUserId, member.UserId, request.CompanyId,
            refuseSelf: rolesChange, cancellationToken);

        if (mayManage.IsError)
        {
            return mayManage.Error!;
        }

        if (rolesChange)
        {
            if (!caller.IsPlatformAdministrator && !caller.Has(PermissionCodes.Roles.Assign))
            {
                return HttpError.Forbidden(
                    $"Changing a member's roles needs the '{PermissionCodes.Roles.Assign}' permission.",
                    errorCode: ErrorCodes.Authorization.PermissionDenied);
            }

            var checkedRoleIds = await StaffRoleRules.CheckAsync(
                unitOfWork, caller, request.RoleIds ?? [], heldRoleIds, nameof(request.RoleIds), cancellationToken);

            if (checkedRoleIds.IsError)
            {
                return checkedRoleIds.Error!;
            }
        }

        var bookabilityChanges = member.IsBookable != request.IsBookable;

        var saved = await Result.TryAsync(
            async () =>
            {
                member.JobTitleCode = jobTitleCode;
                member.IsBookable = request.IsBookable;
                member.JoinedAt = StaffProfileRules.ToJoinedAtInstant(joinedOn!.Value, timeZone);

                if (rolesChange)
                {
                    await StaffRoleRules.ReplaceGrantsAsync(
                        _userRoleRepo, member.UserId, request.CompanyId, currentGrants, wantedRoleIds, cancellationToken);
                }

                if (bookabilityChanges)
                {
                    await outbox.EnqueueAsync(
                        new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The staff member could not be saved. Please try again.",
                errorCode: ErrorCodes.Staff.UpdateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        if (rolesChange)
        {
            await cacheInvalidator.InvalidateUserAsync(member.UserId, cancellationToken);
        }

        return await StaffMemberReader.ReadAsync(
            unitOfWork,
            new StaffViewer(
                request.CompanyId, request.CallerUserId, caller, permissionResolver,
                fileStorageOptions.Value.PublicBaseUrl, timeProvider.GetUtcNow().UtcDateTime),
            member.Id,
            cancellationToken);
    }
}
