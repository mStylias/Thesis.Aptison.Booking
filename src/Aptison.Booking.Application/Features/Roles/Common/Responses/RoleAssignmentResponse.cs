namespace Aptison.Booking.Application.Features.Roles.Common.Responses;

public record RoleAssignmentResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    Guid RoleId,
    string RoleName,
    Guid CompanyId,
    DateTime GrantedAt);
