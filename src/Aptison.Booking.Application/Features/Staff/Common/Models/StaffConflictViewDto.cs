
namespace Aptison.Booking.Application.Features.Staff.Common.Models;

public record StaffConflictViewDto(
    Guid AppointmentId,
    DateOnly LocalDate,
    string LocalStartTime,
    string LocalEndTime,
    string CustomerDisplayName);
