using Aptison.Booking.Application.Features.Availability.Notifications;

namespace Aptison.Booking.Application.Features.Appointments.Common;

internal static class AppointmentCalendar
{
    /// <summary>The days a blocked range touches: both ends, since it can cross local midnight.</summary>
    public static CompanyCalendarChanged CreateChange(
        Guid companyId, DateTime blockedFrom, DateTime blockedUntil, TimeZoneInfo timeZone) =>
        new(
            companyId,
            DateOnly.FromDateTime(ShopTime.ToLocal(blockedFrom, timeZone)),
            DateOnly.FromDateTime(ShopTime.ToLocal(blockedUntil, timeZone)));
}
