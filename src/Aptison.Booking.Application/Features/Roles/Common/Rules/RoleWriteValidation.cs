using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Readers;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Common.Rules;

/// <summary>
/// The shape rules and the read-back that create and update share. Authority is
/// <see cref="RoleGrantRules"/>'s.
/// </summary>
internal static class RoleWriteValidation
{
    // Matches roles.name varchar(64), so a longer name is a 400 rather than a driver error.
    private const int NameMaxLength = 64;

    public static Result<HttpError> Check(string? name, IReadOnlyList<string>? permissions)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var normalizedCodes = permissions ?? [];

        return new Validation("The role is invalid.")
            .Require(trimmed.Length > 0,
                ErrorCodes.Roles.InvalidName, "A role name is required.", nameof(name))
            .Require(trimmed.Length <= NameMaxLength,
                ErrorCodes.Roles.InvalidName, $"A role name may be at most {NameMaxLength} characters.",
                nameof(name))
            .Require(normalizedCodes.All(code => !string.IsNullOrWhiteSpace(code)),
                ErrorCodes.Roles.UnknownPermission, "A permission code must not be blank.",
                nameof(permissions))
            .ToResult();
    }

    /// <summary>
    /// Trims, drops blanks and de-duplicates — <c>role_permissions</c> has a composite key and
    /// would reject a repeated code.
    /// </summary>
    public static IReadOnlyCollection<string> NormalizeCodes(IReadOnlyList<string>? permissions) =>
        (permissions ?? [])
        .Select(code => code.Trim())
        .Where(code => code.Length > 0)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Refuses a name already used by a role this scope can see, the platform's included. Wider
    /// than the database's filtered unique indexes, which cover the two owner classes separately
    /// and would let a company role shadow a platform one. They remain the backstop for the race.
    /// </summary>
    public static async Task<Result<HttpError>> IsNameTakenAsync(
        IRepository<Role, BookingContext> roles,
        string name,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        var normalized = name.ToLowerInvariant();

        var taken = await Result.TryAsync(
            () => roles
                .Query(q => q.Where(role =>
                    role.Name.ToLower() == normalized && (excluding == null || role.Id != excluding)))
                .AnyAsync(cancellationToken),
            ex => HttpError.Internal("Failed to check the role name.", errorCode: ErrorCodes.Roles.FetchFailed));

        if (taken.IsError)
        {
            return taken.Error!;
        }

        return taken.Data
            ? HttpError.Conflict(
                $"A role named '{name}' already exists here.", errorCode: ErrorCodes.Roles.NameTaken)
            : Result.Ok();
    }

    /// <summary>
    /// Re-reads through the same path the read endpoints use, so a write and a subsequent GET
    /// cannot disagree.
    /// </summary>
    public static async Task<Result<RoleResponse, HttpError>> ReadBackAsync(
        IRepository<Role, BookingContext> roles,
        IRepository<RolePermission, BookingContext> rolePermissions,
        Guid roleId,
        Guid? companyId,
        ICurrentPermissions caller,
        CancellationToken cancellationToken)
    {
        var written = await RoleReader.ReadAsync(
            roles, rolePermissions, q => q.Where(role => role.Id == roleId), companyId, caller,
            cancellationToken);

        if (written.IsError)
        {
            return written.Error!;
        }

        return written.Data!.Count > 0
            ? written.Data![0]
            : HttpError.NotFound("The specified role was not found.", errorCode: ErrorCodes.Roles.NotFound);
    }
}
