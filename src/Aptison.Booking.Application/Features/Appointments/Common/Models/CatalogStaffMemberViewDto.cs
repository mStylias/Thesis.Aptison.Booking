namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public record CatalogStaffMemberViewDto(Guid Id, IReadOnlyList<StaffServiceOfferingViewDto> Offerings);
