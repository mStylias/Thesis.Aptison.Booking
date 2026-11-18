using Aptison.Booking.Application.Features.Appointments.Common.Responses;

namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>
/// Either the moved appointment, or — when the move would cost a different total than the one the
/// customer confirmed — that total, with nothing written. The endpoint turns the second into a 409.
/// </summary>
public sealed record AppointmentRescheduleDto(
    AppointmentResponse? Appointment,
    decimal? ChangedTotalPrice,
    string Currency);
