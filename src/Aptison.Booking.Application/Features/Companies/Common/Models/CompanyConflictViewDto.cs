namespace Aptison.Booking.Application.Features.Companies.Common.Models;

public record CompanyConflictViewDto(
    Guid AppointmentId,
    DateOnly LocalDate,
    string LocalStartTime,
    string LocalEndTime,
    string CustomerDisplayName,
    string StaffMemberDisplayName);
