using System.Globalization;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;

namespace Aptison.Booking.Application.Features.Appointments.Common;

/// <summary>How every appointment email writes a language, an amount, a time and a place.</summary>
internal static class AppointmentEmailFormats
{
    // Greek-first: an absent or unrecognised locale falls to Greek.
    public static bool IsGreek(string? languageCode) =>
        !string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase);

    // en-US would price the appointment in dollars; the culture supplies placement, never the symbol.
    public static CultureInfo CreateCulture(bool greek)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(greek ? "el-GR" : "en-US").Clone();

        culture.NumberFormat.CurrencySymbol = "€";

        return culture;
    }

    /// <summary>Falls back to the ISO code when the currency has no symbol we know.</summary>
    public static string FormatMoney(decimal amount, string currency, CultureInfo culture)
    {
        var trimmed = currency.Trim();

        return trimmed.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            ? amount.ToString("C", culture)
            : string.Create(culture, $"{amount:0.00} {trimmed}");
    }

    /// <summary>
    /// Shop-local throughout — every barbershop is in Greece, so the mail never says which clock it
    /// means. With a length, the end follows the start, so a changed end shows in the mail.
    /// </summary>
    public static string FormatWhen(
        DateTime startsAtUtc, string timezone, CultureInfo culture, bool greek, int? durationMinutes = null)
    {
        var timeZone = BookingCompanyReader.GetTimeZone(timezone);
        var localStart = ShopTime.ToLocal(startsAtUtc, timeZone);
        var when = $"{localStart.ToString("dddd d MMMM", culture)}, {FormatClock(localStart, culture, greek)}";

        return durationMinutes is { } minutes
            ? $"{when} – {FormatClock(ShopTime.ToLocal(startsAtUtc.AddMinutes(minutes), timeZone), culture, greek)}"
            : when;
    }

    public static string FormatPlace(string companyName, string companyAddress) =>
        string.IsNullOrWhiteSpace(companyAddress) ? companyName : $"{companyName}, {companyAddress}";

    /// <summary>"Πέμπτη 15 Οκτωβρίου", in the shop's timezone.</summary>
    public static string FormatDate(DateTime startsAtUtc, string timezone, CultureInfo culture) =>
        ShopTime.ToLocal(startsAtUtc, BookingCompanyReader.GetTimeZone(timezone)).ToString("dddd d MMMM", culture);

    public static string FormatTime(DateTime startsAtUtc, string timezone, CultureInfo culture, bool greek) =>
        FormatClock(ShopTime.ToLocal(startsAtUtc, BookingCompanyReader.GetTimeZone(timezone)), culture, greek);

    /// <summary>"today", "tomorrow", or the weekday and date — counted in the shop's timezone from <paramref name="nowUtc"/>.</summary>
    public static RelativeDay FormatRelativeDay(
        DateTime startsAtUtc, DateTime nowUtc, string timezone, CultureInfo culture, bool greek)
    {
        var timeZone = BookingCompanyReader.GetTimeZone(timezone);
        var localStart = ShopTime.ToLocal(startsAtUtc, timeZone);
        var daysAhead = localStart.Date.Subtract(ShopTime.ToLocal(nowUtc, timeZone).Date).Days;

        if (daysAhead is 0 or 1)
        {
            var label = (daysAhead, greek) switch
            {
                (0, true) => "σήμερα",
                (_, true) => "αύριο",
                (0, false) => "today",
                _ => "tomorrow"
            };

            return new RelativeDay(label, label);
        }

        var date = localStart.ToString("dddd d MMMM", culture);

        // Every Greek weekday is feminine except Σάββατο.
        var phrase = greek
            ? $"{(localStart.DayOfWeek == DayOfWeek.Saturday ? "το" : "την")} {date}"
            : $"on {date}";

        return new RelativeDay(date, phrase);
    }

    // The meridiem is spelled out rather than taken from the culture, which gives "AM" or "am"
    // depending on the ICU version behind it.
    private static string FormatClock(DateTime local, CultureInfo culture, bool greek)
    {
        var meridiem = (local.Hour < 12, greek) switch
        {
            (true, true) => "πμ",
            (false, true) => "μμ",
            (true, false) => "am",
            (false, false) => "pm"
        };

        return $"{local.ToString("h:mm", culture)} {meridiem}";
    }
}
