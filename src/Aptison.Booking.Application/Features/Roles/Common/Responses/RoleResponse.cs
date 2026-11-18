namespace Aptison.Booking.Application.Features.Roles.Common.Responses;

/// <summary>
/// A role as one scope sees it. <c>OwnerId</c> is who administers the definition, not where a grant
/// applies. No mapper profile: <c>Permissions</c> comes from a second query and <c>IsEditable</c>
/// from the caller's scope, so neither exists in SQL.
/// </summary>
public record RoleResponse(
    Guid Id,
    Guid? OwnerId,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsEditable,
    IReadOnlyList<string> Permissions);
