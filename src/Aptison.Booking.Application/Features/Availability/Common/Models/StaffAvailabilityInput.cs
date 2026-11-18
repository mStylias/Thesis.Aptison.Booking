namespace Aptison.Booking.Application.Features.Availability.Common.Models;

/// <summary>What one staff member can do, and when they are already spoken for.</summary>
internal sealed record StaffAvailabilityInput(
    Guid StaffMemberId,
    IReadOnlyList<WeeklyWindow> WorkHours,
    IReadOnlyList<BusyWindow> Busy,
    int ShortestOfferingMinutes);
