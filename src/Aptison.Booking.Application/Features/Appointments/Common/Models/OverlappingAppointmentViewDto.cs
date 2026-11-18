namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>Enough to name the other appointment. Times are shop-local <c>HH:mm</c>.</summary>
public record OverlappingAppointmentViewDto(
    Guid Id,
    string CustomerDisplayName,
    string StaffDisplayName,
    string LocalStartTime,
    string LocalEndTime);
