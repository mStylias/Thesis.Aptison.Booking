namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>The appointment a customer is moving, as its barber's calendar holds it now.</summary>
internal sealed record MovedAppointment(Guid Id, Guid StaffMemberId, DateTime BlockedFrom, DateTime BlockedUntil);
