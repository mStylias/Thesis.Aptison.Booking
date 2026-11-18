using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Messaging;

namespace Aptison.Booking.Application.Features.Reminders.Common;

internal static class ReminderLinks
{
    /// <summary>The web app's unsubscribe page, which asks for a press. Null without the app's address.</summary>
    public static EmailLink? CreateUnsubscribePage(string token, string languageCode, string? appUrl)
    {
        if (EmailShell.GetAppOrigin(appUrl) is not { } origin)
        {
            return null;
        }

        var greek = AppointmentEmailFormats.IsGreek(languageCode);

        return new EmailLink(
            greek ? "Σταμάτησε τις υπενθυμίσεις με email" : "Stop reminder emails",
            $"{origin}/{GetLanguage(languageCode)}/reminders/unsubscribe?token={Uri.EscapeDataString(token)}");
    }

    /// <summary>
    /// One-click (RFC 8058) when the API's own address is configured: the mail provider posts there
    /// and nothing asks for a press. Otherwise the page.
    /// </summary>
    public static EmailUnsubscribe? CreateHeader(string token, EmailLink? page, string? apiUrl)
    {
        if (EmailShell.GetAppOrigin(apiUrl) is { } apiOrigin)
        {
            return new EmailUnsubscribe(
                $"{apiOrigin}/api/v1/reminders/unsubscribe/one-click?token={Uri.EscapeDataString(token)}",
                OneClick: true);
        }

        return page is null ? null : new EmailUnsubscribe(page.Url, OneClick: false);
    }

    /// <summary>A path on the web app, which the service worker opens.</summary>
    public static string GetAppointmentPath(Guid appointmentId, string languageCode) =>
        $"/{GetLanguage(languageCode)}/account/appointments/{appointmentId:D}";

    public static string GetSettingsPath(string languageCode) => $"/{GetLanguage(languageCode)}/account/settings";

    private static string GetLanguage(string languageCode) =>
        AppointmentEmailFormats.IsGreek(languageCode) ? "el" : "en";
}
