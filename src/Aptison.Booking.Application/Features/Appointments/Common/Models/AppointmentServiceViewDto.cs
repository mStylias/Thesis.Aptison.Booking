namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public record AppointmentServiceViewDto(
    Guid ServiceId, string Name, decimal Price, int DurationMinutes);
