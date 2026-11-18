using Aptison.Booking.Application.Features.Availability.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>A proposed staff booking and what the shop's schedule says about that day.</summary>
internal sealed record AppointmentCheckInput(
    TimeOnly LocalStart,
    TimeOnly LocalEnd,
    DateTime StartsAt,
    DateTime UtcNow,
    IReadOnlyList<LocalWindow> OpenWindows,
    IReadOnlyList<LocalWindow> WorkWindows,
    bool OverlapsTimeOff,
    IReadOnlyList<OverlappingAppointmentViewDto> CustomerOverlaps,
    IReadOnlyList<OverlappingAppointmentViewDto> SlotsTaken);
