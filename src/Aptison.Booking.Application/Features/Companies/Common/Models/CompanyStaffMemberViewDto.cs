namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>Deliberately no full last name, email or phone: this is served to anyone.</summary>
public record CompanyStaffMemberViewDto(Guid Id, string DisplayName, string Initials, string? AvatarUrl);
