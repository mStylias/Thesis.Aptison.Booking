using System.Globalization;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Reminders.Common;

/// <summary>
/// What a push says. It shows on a locked phone, so it carries no price, note or address. The
/// barber's name stands alone: a Greek "με τον/την" needs a gender and a case we do not store.
/// </summary>
internal static class ReminderPushContent
{
    public static PushContent CreateReminder(AppointmentSummary summary, string kind, DateTime nowUtc)
    {
        var greek = AppointmentEmailFormats.IsGreek(summary.LanguageCode);
        var culture = AppointmentEmailFormats.CreateCulture(greek);
        var time = AppointmentEmailFormats.FormatTime(summary.StartsAt, summary.Timezone, culture, greek);

        string title;

        if (kind == AppointmentReminderKind.TwoHours)
        {
            title = greek ? $"Σε 2 ώρες, στις {time}" : $"In 2 hours, at {time}";
        }
        else
        {
            var day = AppointmentEmailFormats.FormatRelativeDay(
                summary.StartsAt, nowUtc, summary.Timezone, culture, greek);
            title = $"{Capitalize(day.Label, culture)} {(greek ? "στις" : "at")} {time}";
        }

        var services = string.Join(", ", summary.Services.Select(service => service.Name).Where(name => name.Length > 0));
        var body = string.Join(" · ", new[] { summary.CompanyName, services, summary.StaffDisplayName }.Where(part => part.Length > 0));

        return new PushContent(
            title,
            body,
            ReminderLinks.GetAppointmentPath(summary.AppointmentId, summary.LanguageCode),
            GetTag(summary.AppointmentId));
    }

    public static PushContent CreateTest(string languageCode)
    {
        var greek = AppointmentEmailFormats.IsGreek(languageCode);

        return new PushContent(
            greek ? "Οι ειδοποιήσεις λειτουργούν" : "Notifications work",
            greek
                ? "Θα λαμβάνεις υπενθύμιση μία μέρα και δύο ώρες πριν από κάθε ραντεβού."
                : "You will get a reminder one day and two hours before each appointment.",
            ReminderLinks.GetSettingsPath(languageCode),
            "reminders-test");
    }

    /// <summary>One per appointment, so a two-hours push replaces a day-before one still on screen.</summary>
    public static string GetTag(Guid appointmentId) => $"appointment-{appointmentId:D}";

    private static string Capitalize(string text, CultureInfo culture) =>
        text.Length == 0 ? text : char.ToUpper(text[0], culture) + text[1..];
}
