namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>
/// Base price and base duration, before any per-barber override. <c>StaffMemberIds</c> is never
/// empty — a service nobody performs is left out of the response entirely.
/// </summary>
public record BookingServiceViewDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    IReadOnlyList<Guid> StaffMemberIds);
