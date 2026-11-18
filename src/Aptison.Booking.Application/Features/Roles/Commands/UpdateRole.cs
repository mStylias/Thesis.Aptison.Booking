using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Commands;

/// <summary>
/// Replaces a company-owned role's name, description and permission set. A full replace, not a
/// patch: over a set, "add these" and "these are the ones" look identical on the wire.
/// </summary>
public class UpdateRoleRequest : ICommand<Result<RoleResponse, HttpError>>
{
    /// <inheritdoc cref="CreateRoleRequest.CompanyId"/>
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    /// <summary>Taken from the route, for the same reason as <see cref="CompanyId"/>.</summary>
    [JsonIgnore]
    public Guid RoleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = [];
}

public class UpdateRoleHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentPermissions caller,
    IPermissionCacheInvalidator cacheInvalidator)
    : ICommandHandler<UpdateRoleRequest, Result<RoleResponse, HttpError>>,
        IWithValidation<UpdateRoleRequest, Result<HttpError>>
{
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<RolePermission, BookingContext> _rolePermissionRepo =
        unitOfWork.GetRepository<RolePermission>();
    private readonly IRepository<Permission, BookingContext> _permissionRepo =
        unitOfWork.GetRepository<Permission>();

    public Task<Result<HttpError>> ValidateAsync(
        UpdateRoleRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(RoleWriteValidation.Check(request.Name, request.Permissions));

    public async Task<Result<RoleResponse, HttpError>> HandleAsync(
        UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleRepo
            .Query(q => q.Where(candidate => candidate.Id == request.RoleId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        var writable = EnsureWritable(role, request.CompanyId);

        if (writable.IsError)
        {
            return writable.Error!;
        }

        var name = request.Name.Trim();

        var nameTaken = await RoleWriteValidation.IsNameTakenAsync(
            _roleRepo, name, excluding: request.RoleId, cancellationToken);

        if (nameTaken.IsError)
        {
            return nameTaken.Error!;
        }

        var permissionCodes = RoleWriteValidation.NormalizeCodes(request.Permissions);

        var catalog = await RoleGrantRules.ResolveCatalogAsync(_permissionRepo, permissionCodes, cancellationToken);

        if (catalog.IsError)
        {
            return catalog.Error!;
        }

        // Against the whole requested set, not the diff: a caller who has since lost a permission
        // must not keep it in the role by resubmitting unchanged.
        var mayGrant = RoleGrantRules.EnsureCallerMayGrant(catalog.Data!, atCompanyScope: true, caller);

        if (mayGrant.IsError)
        {
            return mayGrant.Error!;
        }

        var existingRolePermissions = await _rolePermissionRepo
            .Query(q => q.Where(rolePermission => rolePermission.RoleId == request.RoleId), asNoTracking: false)
            .ToListAsync(cancellationToken);

        var wantedPermissionIds = catalog.Data!.Select(permission => permission.Id).ToHashSet();
        var heldPermissionIds = existingRolePermissions.Select(rolePermission => rolePermission.PermissionId).ToHashSet();

        var saved = await Result.TryAsync(
            async () =>
            {
                role!.Name = name;
                role.Description = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim();

                _roleRepo.Update(role);

                foreach (var removed in existingRolePermissions.Where(rolePermission => !wantedPermissionIds.Contains(rolePermission.PermissionId)))
                {
                    // Hard delete: role_permissions has no soft-delete columns, and the audit trail
                    // for "who changed this role" is on the role row.
                    _rolePermissionRepo.Remove(removed);
                }

                foreach (var added in wantedPermissionIds.Where(permissionId => !heldPermissionIds.Contains(permissionId)))
                {
                    await _rolePermissionRepo.AddAsync(
                        new RolePermission { RoleId = request.RoleId, PermissionId = added },
                        cancellationToken);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            ex => HttpError.Internal("Failed to update the role.", errorCode: ErrorCodes.Roles.UpdateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        // Unconditional, even for a rename: deciding here whether an edit "mattered" turns
        // immediate revocation into eventual revocation the first time the decision is wrong.
        await cacheInvalidator.InvalidateRoleAsync(request.RoleId, cancellationToken);

        return await RoleWriteValidation.ReadBackAsync(
            _roleRepo, _rolePermissionRepo, request.RoleId, request.CompanyId, caller, cancellationToken);
    }

    /// <summary>The two invariants that make a role readable but not writable.</summary>
    internal static Result<HttpError> EnsureWritable(Role? role, Guid companyId)
    {
        if (role is null)
        {
            return HttpError.NotFound(
                "The specified role was not found.", errorCode: ErrorCodes.Roles.NotFound);
        }

        if (role.IsSystem)
        {
            return HttpError.Forbidden(
                $"'{role.Name}' is a system role and cannot be renamed, re-scoped or deleted.",
                errorCode: ErrorCodes.Roles.SystemRoleImmutable);
        }

        // Reachable: the ownership filter shows a company its own roles and the platform's.
        if (role.OwnerId != companyId)
        {
            return HttpError.Forbidden(
                "This role is provided by the platform. A company may use it, and may only edit the " +
                "roles it owns.",
                errorCode: ErrorCodes.Roles.NotOwnedByCompany);
        }

        return Result.Ok();
    }
}
