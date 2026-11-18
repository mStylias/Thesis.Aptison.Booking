namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>One of a staff member's upcoming appointments, for the schedule-conflict check.</summary>
internal sealed record StaffUpcomingAppointmentDto(Guid Id, DateTime StartsAt, DateTime EndsAt, string CustomerDisplayName);
