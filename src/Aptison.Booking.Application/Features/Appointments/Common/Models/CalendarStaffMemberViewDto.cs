namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public record CalendarStaffMemberViewDto(
    Guid Id, string DisplayName, string Initials, string? AvatarUrl, bool IsActive);
