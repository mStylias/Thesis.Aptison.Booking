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
/// Creates a role a company owns and administers. It hands out authority at runtime, so the rules
/// in <see cref="RoleGrantRules"/> are what stop an administrator minting themselves the platform.
/// </summary>
public class CreateRoleRequest : ICommand<Result<RoleResponse, HttpError>>
{
    /// <summary>
    /// The request's resolved company. <c>JsonIgnore</c> so a body naming a different company is
    /// unreachable rather than merely overwritten.
    /// </summary>
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Empty is legal and grants nothing; it never means "all".</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];
}

public class CreateRoleHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentPermissions caller,
    IPermissionCacheInvalidator cacheInvalidator)
    : ICommandHandler<CreateRoleRequest, Result<RoleResponse, HttpError>>,
        IWithValidation<CreateRoleRequest, Result<HttpError>>
{
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<RolePermission, BookingContext> _rolePermissionRepo =
        unitOfWork.GetRepository<RolePermission>();
    private readonly IRepository<Permission, BookingContext> _permissionRepo =
        unitOfWork.GetRepository<Permission>();

    public Task<Result<HttpError>> ValidateAsync(
        CreateRoleRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(RoleWriteValidation.Check(request.Name, request.Permissions));

    public async Task<Result<RoleResponse, HttpError>> HandleAsync(
        CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var nameTaken = await RoleWriteValidation.IsNameTakenAsync(_roleRepo, name, excluding: null, cancellationToken);

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

        var mayGrant = RoleGrantRules.EnsureCallerMayGrant(catalog.Data!, atCompanyScope: true, caller);

        if (mayGrant.IsError)
        {
            return mayGrant.Error!;
        }

        var role = new Role
        {
            Id = Guid.CreateVersion7(),
            // Explicit, though the interceptor would stamp the same value — so this stays correct
            // if ever called from a path with no ambient company.
            OwnerId = request.CompanyId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false
        };

        var saved = await Result.TryAsync(
            async () =>
            {
                await _roleRepo.AddAsync(role, cancellationToken);

                foreach (var permission in catalog.Data!)
                {
                    await _rolePermissionRepo.AddAsync(
                        new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
                        cancellationToken);
                }

                // One SaveChanges, so no TransactionBehavior: one wrapping the handler would still
                // be open when the invalidation below runs, which is the ordering to avoid.
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            ex => HttpError.Internal("Failed to create the role.", errorCode: ErrorCodes.Roles.CreateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        // Nobody holds it yet, but a role-map fill in flight during this insert would write a map
        // that predates it.
        await cacheInvalidator.InvalidateRoleAsync(role.Id, cancellationToken);

        return await RoleWriteValidation.ReadBackAsync(
            _roleRepo, _rolePermissionRepo, role.Id, request.CompanyId, caller, cancellationToken);
    }
}
