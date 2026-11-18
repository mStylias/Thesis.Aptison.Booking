
namespace Aptison.Booking.Application.Features.Users.Common.Models;

/// <summary>A member of the company's staff, inactive ones included, named as on the management calendar.</summary>
public record WorkspaceStaffMemberViewDto(Guid Id, string DisplayName, bool IsActive);
