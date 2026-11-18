namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public record BookableStaffMemberViewDto(
    Guid Id,
    string DisplayName,
    string Initials,
    string? AvatarUrl,
    IReadOnlyList<StaffServiceOfferingViewDto> Offerings);
