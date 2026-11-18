using System.Globalization;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Messaging;

namespace Aptison.Booking.Application.Features.Appointments.Common;

internal static class AppointmentLinks
{
    /// <summary>
    /// The appointment in the customer's account, or the shop's page for a walk-in, who has no
    /// account to open it in. Null when the web app's address is not configured.
    /// </summary>
    public static AppointmentViewLink? Create(AppointmentSummary summary, string? appUrl)
    {
        if (EmailShell.GetAppOrigin(appUrl) is not { } origin)
        {
            return null;
        }

        var greek = AppointmentEmailFormats.IsGreek(summary.LanguageCode);
        var language = greek ? "el" : "en";

        return summary.CustomerHasAccount
            ? new AppointmentViewLink(
                greek ? "Δες το ραντεβού" : "View appointment",
                $"{origin}/{language}/account/appointments/{summary.AppointmentId:D}",
                EmailShell.GetIcon(appUrl, "calendar-check.png", 20, 20))
            : new AppointmentViewLink(
                greek ? "Δες το κατάστημα" : "View barbershop",
                $"{origin}/{language}/barbershops/{Uri.EscapeDataString(summary.CompanySlug)}",
                EmailShell.GetIcon(appUrl, "store.png", 20, 20));
    }

    /// <summary>The Google Maps pin, as on the web app's map buttons. Null when the web app's address is not configured.</summary>
    public static EmailIcon? CreateMapIcon(string? appUrl) => EmailShell.GetIcon(appUrl, "google-maps.png", 14, 20);

    /// <summary>Google Maps at the shop's coordinates; it opens the Maps app where one is installed.</summary>
    public static EmailLink CreateMap(AppointmentSummary summary) =>
        new(
            AppointmentEmailFormats.IsGreek(summary.LanguageCode) ? "Δες τον χάρτη" : "View on map",
            string.Create(
                CultureInfo.InvariantCulture,
                $"https://www.google.com/maps/search/?api=1&query={summary.CompanyLatitude:0.######},{summary.CompanyLongitude:0.######}"));
}
