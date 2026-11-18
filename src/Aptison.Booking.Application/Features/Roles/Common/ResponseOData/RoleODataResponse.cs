namespace Aptison.Booking.Application.Features.Roles.Common.ResponseOData;

/// <summary>
/// A company's role as an OData grid reads it. Every member must stay SQL-translatable — OData
/// filters against the projection, so anything composed in memory is invisible to <c>$filter</c>.
/// <c>NumberOfUsers</c> counts grants at this company only.
/// </summary>
public record RoleODataResponse(
    Guid Id,
    Guid? OwnerId,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsEditable,
    int NumberOfUsers);
