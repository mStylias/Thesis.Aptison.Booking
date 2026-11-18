namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>
/// <c>Time</c> is shop-local <c>HH:mm</c>; <c>StartsAtUtc</c> is echoed back verbatim when booking,
/// which keeps every timezone and DST decision on this side of the wire. <c>StaffMemberIds</c> is
/// who is free at that instant for the whole selection, and is never empty.
/// </summary>
public record AvailableSlotViewDto(
    string Time, string StartsAtUtc, IReadOnlyList<Guid> StaffMemberIds);
