using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

internal static class BusinessHourRules
{
    /// <summary>Stored days count from <c>0</c> = Sunday. The views are ISO, <c>1</c> = Monday to <c>7</c> = Sunday.</summary>
    public static List<BusinessHourViewDto> ToViews(IEnumerable<(short DayOfWeek, TimeOnly Open, TimeOnly Close)> hours) =>
        [.. hours
            .Select(hour => new BusinessHourViewDto(
                hour.DayOfWeek == 0 ? 7 : hour.DayOfWeek,
                BookingFormats.FormatTimeOfDay(hour.Open),
                BookingFormats.FormatTimeOfDay(hour.Close)))
            .OrderBy(hour => hour.DayOfWeek)
            .ThenBy(hour => hour.OpenTime, StringComparer.Ordinal)];
}
