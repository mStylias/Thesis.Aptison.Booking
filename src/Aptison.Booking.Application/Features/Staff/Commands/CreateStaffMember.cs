using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Persistence;
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
using Aptison.Booking.Domain.Entities.Services;
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

/// <summary>
/// Adds a person who already has an account. Never creates a user or a login. The client sends the
/// member's services and hours; nothing is copied from the shop.
/// </summary>
public class CreateStaffMemberRequest : ICommand<Result<StaffMemberResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    public string? Email { get; set; }
    public string? JobTitleCode { get; set; }
    public bool IsBookable { get; set; } = true;

    /// <summary>Shop-local <c>yyyy-MM-dd</c>; today when null.</summary>
    public string? JoinedAt { get; set; }

    public IReadOnlyList<Guid> RoleIds { get; set; } = [];
    public IReadOnlyList<StaffServiceAddEditDto> Services { get; set; } = [];
    public IReadOnlyList<StaffWorkHourAddEditDto> WorkHours { get; set; } = [];
}

public sealed record CreateStaffMemberInput(
    Guid UserId,
    string? JobTitleCode,
    DateTime JoinedAt,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<StaffServiceInput> Services,
    IReadOnlyList<StaffWorkHourInput> WorkHours);

/// <remarks>
/// One <c>SaveChangesAsync</c>, and no transaction behavior: the permission cache is evicted after
/// the commit, and the behavior's transaction would still be open.
/// </remarks>
public class CreateStaffMemberHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IUserContextScope userContext,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IPermissionCacheInvalidator cacheInvalidator,
    IDatabaseConstraintReader constraintReader,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : ICommandHandler<CreateStaffMemberRequest, Result<StaffMemberResponse, HttpError>>,
        IWithValidation<CreateStaffMemberRequest, Result<CreateStaffMemberInput, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<UserRole, BookingContext> _userRoleRepo = unitOfWork.GetRepository<UserRole>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();
    private readonly IRepository<StaffWorkHour, BookingContext> _workHourRepo =
        unitOfWork.GetRepository<StaffWorkHour>();

    public async Task<Result<CreateStaffMemberInput, HttpError>> ValidateAsync(
        CreateStaffMemberRequest request, CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), request.CompanyId, cancellationToken);
        var timeZone = BookingCompanyReader.GetTimeZone(company?.Timezone ?? "UTC");
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var validation = new Validation("The staff member is invalid.");
        var jobTitleCode = StaffProfileRules.CheckJobTitle(validation, request.JobTitleCode, nameof(request.JobTitleCode));
        var joinedOn = request.JoinedAt is null
            ? StaffProfileRules.ToJoinedAtDay(utcNow, timeZone)
            : StaffProfileRules.CheckJoinedAt(validation, request.JoinedAt, nameof(request.JoinedAt));

        StaffWorkHourRules.Check(validation, request.WorkHours ?? [], nameof(request.WorkHours), out var workHours);

        var services = await StaffServiceRules.CheckAsync(
            validation, unitOfWork.GetRepository<Service>(), request.Services ?? [], nameof(request.Services),
            cancellationToken);

        if (validation.HasErrors)
        {
            return validation.Error;
        }

        var roleIds = request.RoleIds ?? [];

        if (roleIds.Count > 0 && !caller.IsPlatformAdministrator && !caller.Has(PermissionCodes.Roles.Assign))
        {
            return HttpError.Forbidden(
                $"Giving a member roles needs the '{PermissionCodes.Roles.Assign}' permission.",
                errorCode: ErrorCodes.Authorization.PermissionDenied);
        }

        var checkedRoleIds = await StaffRoleRules.CheckAsync(
            unitOfWork, caller, roleIds, heldRoleIds: [], nameof(request.RoleIds), cancellationToken);

        if (checkedRoleIds.IsError)
        {
            return checkedRoleIds.Error!;
        }

        var candidate = await StaffCandidateReader.FindAsync(
            unitOfWork, userContext, request.CompanyId, request.Email, nameof(request.Email), utcNow, cancellationToken);

        if (candidate.IsError)
        {
            return candidate.Error!;
        }

        return new CreateStaffMemberInput(
            candidate.Data!.UserId,
            jobTitleCode,
            StaffProfileRules.ToJoinedAtInstant(joinedOn!.Value, timeZone),
            checkedRoleIds.Data!,
            services,
            workHours);
    }

    public async Task<Result<StaffMemberResponse, HttpError>> HandleAsync(
        CreateStaffMemberRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();

        var member = new StaffMember
        {
            Id = Guid.CreateVersion7(),
            OwnerId = request.CompanyId,
            UserId = input.UserId,
            JobTitleCode = input.JobTitleCode,
            IsBookable = request.IsBookable,
            IsActive = true,
            JoinedAt = input.JoinedAt
        };

        var saved = await SaveAsync(request, member, input, cancellationToken);

        if (saved.IsError)
        {
            return saved.Error!;
        }

        // After the commit: the user now has a company, and their cached entry says they do not.
        await cacheInvalidator.InvalidateUserAsync(input.UserId, cancellationToken);

        return await StaffMemberReader.ReadAsync(
            unitOfWork,
            new StaffViewer(
                request.CompanyId, request.CallerUserId, caller, permissionResolver,
                fileStorageOptions.Value.PublicBaseUrl, timeProvider.GetUtcNow().UtcDateTime),
            member.Id,
            cancellationToken);
    }

    private async Task<Result<HttpError>> SaveAsync(
        CreateStaffMemberRequest request, StaffMember member, CreateStaffMemberInput input, CancellationToken cancellationToken)
    {
        try
        {
            await _staffMemberRepo.AddAsync(member, cancellationToken);

            await StaffRoleRules.ReplaceGrantsAsync(
                _userRoleRepo, input.UserId, request.CompanyId, [], input.RoleIds, cancellationToken);

            await StaffServiceRules.WriteAsync(
                _staffServiceRepo, request.CompanyId, member.Id, [], input.Services, cancellationToken);

            foreach (var hour in input.WorkHours)
            {
                await _workHourRepo.AddAsync(
                    StaffWorkHourRules.CreateRow(request.CompanyId, member.Id, hour), cancellationToken);
            }

            await outbox.EnqueueAsync(
                new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, StaffConstraints.OneActiveEmploymentPerUser))
        {
            // Added here or elsewhere a moment ago; the lookup says which.
            var candidate = await StaffCandidateReader.FindAsync(
                unitOfWork, userContext, request.CompanyId, request.Email, nameof(request.Email),
                timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

            return candidate.IsError ? candidate.Error! : StaffCandidateReader.CreateEmployedElsewhereError();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The staff member could not be added. Please try again.", errorCode: ErrorCodes.Staff.CreateFailed);
        }
    }
}
