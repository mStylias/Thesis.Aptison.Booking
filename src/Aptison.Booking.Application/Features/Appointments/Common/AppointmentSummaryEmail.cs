using System.Globalization;
using System.Net;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Messaging;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Appointments.Common;

/// <summary>The email that states an appointment in full: when, where, who, what and how much.</summary>
internal static class AppointmentSummaryEmail
{
    /// <param name="isChange">A change rather than a booking, which changes only the heading.</param>
    public static EmailMessage Compose(AppointmentSummary summary, bool isChange, EmailBrandingOptions branding)
    {
        var greek = AppointmentEmailFormats.IsGreek(summary.LanguageCode);
        var culture = AppointmentEmailFormats.CreateCulture(greek);

        return Build(summary, Copy.CreateNotice(greek, isChange, culture, summary), culture, greek, branding, footerLink: null);
    }

    /// <param name="kind">One of <see cref="AppointmentReminderKind"/>.</param>
    /// <param name="unsubscribe">The footer link; null drops it, when the web app's address is not configured.</param>
    public static EmailMessage ComposeReminder(
        AppointmentSummary summary,
        string kind,
        DateTime nowUtc,
        EmailLink? unsubscribe,
        EmailBrandingOptions branding)
    {
        var greek = AppointmentEmailFormats.IsGreek(summary.LanguageCode);
        var culture = AppointmentEmailFormats.CreateCulture(greek);
        var copy = Copy.CreateReminder(greek, kind, nowUtc, culture, summary);

        return Build(summary, copy, culture, greek, branding, unsubscribe);
    }

    private static EmailMessage Build(
        AppointmentSummary summary,
        Copy copy,
        CultureInfo culture,
        bool greek,
        EmailBrandingOptions branding,
        EmailLink? footerLink)
    {
        var link = AppointmentLinks.Create(summary, branding.AppUrl);
        var map = AppointmentLinks.CreateMap(summary);

        return new EmailMessage(
            To: summary.Email,
            Subject: copy.Subject,
            HtmlBody: BuildHtml(summary, copy, link, map, culture, greek, branding, footerLink),
            TextBody: BuildText(summary, copy, link, map, culture, footerLink));
    }

    /// <summary>The card's interior; <see cref="EmailShell"/> supplies the frame.</summary>
    private static string BuildHtml(
        AppointmentSummary summary,
        Copy copy,
        AppointmentViewLink? link,
        EmailLink map,
        CultureInfo culture,
        bool greek,
        EmailBrandingOptions branding,
        EmailLink? footerLink)
    {
        var content = $"""
                      <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                        <tr>
                          <td style="font-family:{EmailShell.Serif}; font-size:26px; line-height:32px; font-weight:700; color:{EmailShell.TextPrimary}; padding:0 0 20px;">
                            {WebUtility.HtmlEncode(copy.Heading)}
                          </td>
                        </tr>
                        <tr>
                          <td>
                      {AppointmentDetailsCard.Build(summary, culture, greek, branding.AppUrl)}
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:28px 0 0;">
                      {BuildButtons(link, map, branding.AppUrl)}
                          </td>
                        </tr>
                      </table>
                      """;

        return EmailShell.Wrap(
            languageTag: greek ? "el" : "en",
            preheader: copy.WhenLine,
            content: content,
            footer: copy.Footer,
            branding: branding,
            footerLink: footerLink);
    }

    // The map alone, at its own width, when the web app's address is not configured.
    private static string BuildButtons(AppointmentViewLink? link, EmailLink map, string? appUrl)
    {
        var mapIcon = AppointmentLinks.CreateMapIcon(appUrl);

        return link is null
            ? EmailShell.BuildButton(map.Label, map.Url, mapIcon, outlined: true)
            : EmailShell.BuildButtonPair(
                primary: EmailShell.BuildButton(link.Label, link.Url, link.Icon, block: true),
                secondary: EmailShell.BuildButton(map.Label, map.Url, mapIcon, outlined: true, block: true));
    }

    // Not optional — see EmailMessage.TextBody.
    private static string BuildText(
        AppointmentSummary summary,
        Copy copy,
        AppointmentViewLink? link,
        EmailLink map,
        CultureInfo culture,
        EmailLink? footerLink)
    {
        var lines = summary.Services.Select(service =>
            $"- {service.Name} · {service.DurationMinutes}′ · " +
            AppointmentEmailFormats.FormatMoney(service.Price, summary.Currency, culture));

        var note = summary.Notes is null
            ? string.Empty
            : $"{copy.NoteLabel} {summary.Notes}{Environment.NewLine}";

        var phone = string.IsNullOrWhiteSpace(summary.CompanyPhone)
            ? string.Empty
            : $"{copy.PhoneLabel} {summary.CompanyPhone}{Environment.NewLine}";

        return $"""
                {copy.Heading}

                {copy.WhenLine}
                {copy.WhereLine}
                {copy.WhoLine}
                {phone}{map.Label}: {map.Url}

                {string.Join(Environment.NewLine, lines)}

                {copy.TotalLabel} {AppointmentEmailFormats.FormatMoney(summary.TotalPrice, summary.Currency, culture)}
                {note}
                {(link is null ? string.Empty : $"{link.Label}: {link.Url}")}

                --
                {copy.Footer}
                {(footerLink is null ? string.Empty : $"{footerLink.Label}: {footerLink.Url}")}
                """;
    }

    /// <summary>
    /// Second person singular in Greek, matching every other surface this app puts in front of a
    /// customer.
    /// </summary>
    private sealed record Copy(
        string Subject,
        string Heading,
        string WhenLine,
        string WhereLine,
        string WhoLine,
        string PhoneLabel,
        string TotalLabel,
        string NoteLabel,
        string Footer)
    {
        public static Copy CreateNotice(bool greek, bool isChange, CultureInfo culture, AppointmentSummary summary)
        {
            var copy = CreateCommon(greek, culture, summary);

            return greek
                ? copy with
                {
                    Subject = isChange
                        ? $"Το ραντεβού σου στο {summary.CompanyName} άλλαξε"
                        : $"Το ραντεβού σου στο {summary.CompanyName} επιβεβαιώθηκε",
                    Heading = isChange ? "Το ραντεβού σου άλλαξε" : "Το ραντεβού σου επιβεβαιώθηκε!"
                }
                : copy with
                {
                    Subject = isChange
                        ? $"Your appointment at {summary.CompanyName} has changed"
                        : $"Your appointment at {summary.CompanyName} is confirmed",
                    Heading = isChange ? "Your appointment has changed" : "Your appointment is confirmed"
                };
        }

        public static Copy CreateReminder(
            bool greek, string kind, DateTime nowUtc, CultureInfo culture, AppointmentSummary summary)
        {
            var copy = CreateCommon(greek, culture, summary);
            var time = AppointmentEmailFormats.FormatTime(summary.StartsAt, summary.Timezone, culture, greek);
            var day = AppointmentEmailFormats.FormatRelativeDay(summary.StartsAt, nowUtc, summary.Timezone, culture, greek);
            var twoHours = kind == AppointmentReminderKind.TwoHours;

            return (greek, twoHours) switch
            {
                (true, true) => copy with
                {
                    Subject = $"Σε 2 ώρες: {time} στο {summary.CompanyName}",
                    Heading = "Το ραντεβού σου είναι σε 2 ώρες",
                    Footer = $"Λαμβάνεις αυτό το email επειδή έχεις ραντεβού στο {summary.CompanyName}."
                },
                (true, false) => copy with
                {
                    Subject = $"Υπενθύμιση: {day.Label} στις {time} στο {summary.CompanyName}",
                    Heading = $"Το ραντεβού σου είναι {day.Phrase}",
                    Footer = $"Λαμβάνεις αυτό το email επειδή έχεις ραντεβού στο {summary.CompanyName}."
                },
                (false, true) => copy with
                {
                    Subject = $"In 2 hours: {time} at {summary.CompanyName}",
                    Heading = "Your appointment is in 2 hours",
                    Footer = $"You are getting this email because you have an appointment at {summary.CompanyName}."
                },
                (false, false) => copy with
                {
                    Subject = $"Reminder: {day.Label} at {time} at {summary.CompanyName}",
                    Heading = $"Your appointment is {day.Phrase}",
                    Footer = $"You are getting this email because you have an appointment at {summary.CompanyName}."
                }
            };
        }

        private static Copy CreateCommon(bool greek, CultureInfo culture, AppointmentSummary summary)
        {
            var when = AppointmentEmailFormats.FormatWhen(
                summary.StartsAt, summary.Timezone, culture, greek, summary.DurationMinutes);
            var place = AppointmentEmailFormats.FormatPlace(summary.CompanyName, summary.CompanyAddress);

            return greek
                ? new Copy(
                    Subject: string.Empty,
                    Heading: string.Empty,
                    WhenLine: when,
                    WhereLine: place,
                    WhoLine: $"Κουρέας: {summary.StaffDisplayName}",
                    PhoneLabel: "Τηλέφωνο:",
                    TotalLabel: "Σύνολο",
                    NoteLabel: "Η σημείωσή σου:",
                    Footer: "Αυτόματο μήνυμα. Μην απαντήσεις σε αυτό.")
                : new Copy(
                    Subject: string.Empty,
                    Heading: string.Empty,
                    WhenLine: when,
                    WhereLine: place,
                    WhoLine: $"Barber: {summary.StaffDisplayName}",
                    PhoneLabel: "Phone:",
                    TotalLabel: "Total",
                    NoteLabel: "Your note:",
                    Footer: "This is an automated message. Please do not reply to it.");
        }
    }
}
