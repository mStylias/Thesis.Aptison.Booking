using System.Globalization;

namespace Aptison.Booking.Application.Features.Appointments.Common;

/// <summary>
/// How the booking contract puts a date, a time of day and an instant on the wire. Shop-local
/// <c>yyyy-MM-dd</c> and <c>HH:mm</c>, and UTC with a <c>Z</c> — the client parses these and never
/// converts between them.
/// </summary>
internal static class BookingFormats
{
    public static string FormatDay(DateOnly day) =>
        day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>From minutes past local midnight, the form the slot index stores.</summary>
    public static string FormatTimeOfDay(int localMinute) =>
        string.Create(CultureInfo.InvariantCulture, $"{localMinute / 60:00}:{localMinute % 60:00}");

    public static string FormatTimeOfDay(TimeOnly time) =>
        time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string FormatInstant(DateTime utc) =>
        utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    public static bool TryParseDay(string? value, out DateOnly day) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    public static bool TryParseTimeOfDay(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Null when any entry is not a guid; an empty list when there is nothing to parse.</summary>
    public static List<Guid>? ParseServiceIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var serviceIds = new List<Guid>();

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var serviceId))
            {
                return null;
            }

            serviceIds.Add(serviceId);
        }

        return serviceIds;
    }
}
