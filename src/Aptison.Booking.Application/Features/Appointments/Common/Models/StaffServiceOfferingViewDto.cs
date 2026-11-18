namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>One service this barber performs, with their own price and duration applied.</summary>
public record StaffServiceOfferingViewDto(Guid ServiceId, decimal Price, int DurationMinutes);
