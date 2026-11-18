using System.Net;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Messaging;
using Aptison.Booking.Application.Messaging.Calendar;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Appointments.Notifications;

/// <summary>
/// An appointment was cancelled, by the shop or by the customer online. <c>Reason</c> is null
/// unless staff chose to include it.
/// </summary>
public sealed record AppointmentCancelled(
    AppointmentSummary Summary,
    string? Reason,
    bool CancelledByCustomer = false) : INotification;

/// <inheritdoc cref="AppointmentBookedHandler"/>
public sealed class AppointmentCancelledHandler(
    IEmailSender emailSender,
    IOptions<EmailBrandingOptions> branding,
    IOptions<EmailSenderOptions> sender,
    TimeProvider timeProvider,
    ILogger<AppointmentCancelledHandler> logger)
    : INotificationHandler<AppointmentCancelled>
{
    public async Task HandleAsync(AppointmentCancelled notification, CancellationToken cancellationToken)
    {
        var summary = notification.Summary;
        var greek = AppointmentEmailFormats.IsGreek(summary.LanguageCode);
        var culture = AppointmentEmailFormats.CreateCulture(greek);
        var when = AppointmentEmailFormats.FormatWhen(summary.StartsAt, summary.Timezone, culture, greek);
        var place = AppointmentEmailFormats.FormatPlace(summary.CompanyName, summary.CompanyAddress);
        var link = AppointmentLinks.Create(summary, branding.Value.AppUrl);

        var subject = (greek, notification.CancelledByCustomer) switch
        {
            (true, true) => $"Ακύρωσες το ραντεβού σου στο {summary.CompanyName}",
            (true, false) => $"Το ραντεβού σου στο {summary.CompanyName} ακυρώθηκε",
            (false, true) => $"You cancelled your appointment at {summary.CompanyName}",
            (false, false) => $"Your appointment at {summary.CompanyName} was cancelled"
        };
        var heading = greek ? "Το ραντεβού σου ακυρώθηκε" : "Your appointment was cancelled";
        var who = greek ? $"Κουρέας: {summary.StaffDisplayName}" : $"Barber: {summary.StaffDisplayName}";
        var reason = notification.Reason is null
            ? null
            : greek ? $"Λόγος: {notification.Reason}" : $"Reason: {notification.Reason}";
        var closing = greek
            ? "Μπορείς να κλείσεις νέο ραντεβού όποτε σε βολεύει."
            : "You are welcome to book again whenever it suits you.";
        var footer = greek
            ? "Αυτόματο μήνυμα. Μην απαντήσεις σε αυτό."
            : "This is an automated message. Please do not reply to it.";

        var buttonRow = link is null
            ? string.Empty
            : $"""
                        <tr>
                          <td align="center" style="padding:28px 0 0;">
               {EmailShell.BuildButton(link.Label, link.Url, link.Icon)}
                          </td>
                        </tr>
               """;

        var content = $"""
                      <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                        <tr>
                          <td style="font-family:{EmailShell.Serif}; font-size:26px; line-height:32px; font-weight:700; color:{EmailShell.TextPrimary}; padding:0 0 20px;">
                            {WebUtility.HtmlEncode(heading)}
                          </td>
                        </tr>
                        <tr>
                          <td>
                      {AppointmentDetailsCard.Build(summary, culture, greek, branding.Value.AppUrl, cancelled: true, reason: notification.Reason)}
                          </td>
                        </tr>
                        <tr>
                          <td style="font-family:{EmailShell.Sans}; font-size:15px; line-height:24px; color:{EmailShell.TextSecondary}; padding:20px 0 0;">
                            {WebUtility.HtmlEncode(closing)}
                          </td>
                        </tr>
                      {buttonRow}
                      </table>
                      """;

        var text = $"""
                    {heading}

                    {when}
                    {place}
                    {who}
                    {(reason is null ? string.Empty : reason + Environment.NewLine)}
                    {closing}
                    {(link is null ? string.Empty : $"{link.Label}: {link.Url}")}

                    --
                    {footer}
                    """;

        var message = new EmailMessage(
            To: summary.Email,
            Subject: subject,
            HtmlBody: EmailShell.Wrap(
                languageTag: greek ? "el" : "en",
                preheader: when,
                content: content,
                footer: footer,
                branding: branding.Value),
            TextBody: text,
            Calendar: AppointmentInvitation.TryCreatePart(
                summary, CalendarMethod.Cancel, notification.Reason, sender.Value.Address, branding.Value.AppUrl,
                timeProvider.GetUtcNow().UtcDateTime, logger));

        await emailSender.SendAsync(message, cancellationToken);

        logger.LogInformation("Delivered the cancellation for appointment {AppointmentId}", summary.AppointmentId);
    }
}
