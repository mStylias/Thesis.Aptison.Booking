namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public sealed record ListAppointmentDaysInput(DateOnly FromDay, DateOnly ToDay, IReadOnlyList<Guid> ServiceIds);
