namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary><c>DurationMinutes</c> is the booking-time snapshot; <c>CategoryId</c> is today's.</summary>
public record AppointmentServiceODataViewDto
{
    public Guid ServiceId { get; init; }
    public string Name { get; init; } = null!;
    public Guid? CategoryId { get; init; }
    public int DurationMinutes { get; init; }
}
