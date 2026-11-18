namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary><c>Appointment</c> is the other appointment, for <c>SLOT_TAKEN</c> and <c>CUSTOMER_OVERLAP</c>.</summary>
public record AppointmentWarningViewDto(string Code, OverlappingAppointmentViewDto? Appointment);
