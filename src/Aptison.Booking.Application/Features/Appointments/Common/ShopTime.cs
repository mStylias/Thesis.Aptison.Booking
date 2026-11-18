namespace Aptison.Booking.Application.Features.Appointments.Common;

/// <summary>Conversions between an instant and a shop's wall clock.</summary>
internal static class ShopTime
{
    public static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);

    /// <summary>Nudges past a local time daylight saving skipped, rather than throwing on it.</summary>
    public static DateTime ToUtcSkippingGap(DateTime local, TimeZoneInfo timeZone)
    {
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
