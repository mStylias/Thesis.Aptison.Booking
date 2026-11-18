using System.Globalization;
using System.Net;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.EmailVerification.Notifications;

/// <summary>
/// Staged in the outbox rather than mailed inline, so a transient SMTP failure does not look like a
/// failed registration with the cooldown already started.
/// <para>
/// <b>The payload holds the code in plaintext in Postgres</b> — the store keeps only a digest. The
/// row is deleted on dispatch, but a dead-lettered one keeps its payload, so treat the outbox table
/// as holding credentials.
/// </para>
/// <c>LanguageCode</c> comes from the token's <c>locale</c> claim: an unverified caller has no
/// <c>users</c> row yet.
/// </summary>
public sealed record EmailVerificationCodeIssued(
    string Email,
    string Code,
    DateTimeOffset ExpiresAt,
    string LanguageCode) : INotification;

/// <summary>
/// Reached by the outbox dispatcher, never called directly. Delivery is at-least-once, so this must
/// tolerate running twice; a throw is the retry signal.
/// </summary>
public sealed class EmailVerificationCodeIssuedHandler(
    IEmailSender emailSender,
    IOptions<EmailBrandingOptions> branding,
    ILogger<EmailVerificationCodeIssuedHandler> logger)
    : INotificationHandler<EmailVerificationCodeIssued>
{
    private readonly EmailBrandingOptions _branding = branding.Value;

    public async Task HandleAsync(
        EmailVerificationCodeIssued notification, CancellationToken cancellationToken)
    {
        var greek = IsGreek(notification.LanguageCode);
        var minutes = Math.Max(1, (int)Math.Round((notification.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes));
        var copy = Copy.Create(greek, minutes);

        var message = new EmailMessage(
            To: notification.Email,
            Subject: greek
                ? $"{notification.Code} — ο κωδικός επιβεβαίωσής σου"
                : $"{notification.Code} is your verification code",
            HtmlBody: BuildHtml(notification.Code, copy, greek),
            TextBody: BuildText(notification.Code, copy));

        await emailSender.SendAsync(message, cancellationToken);

        // The address, never the code.
        logger.LogInformation("Delivered a verification code to {Recipient}", notification.Email);
    }

    // Greek-first: an absent or unrecognised locale falls to Greek.
    private static bool IsGreek(string? languageCode) =>
        !string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase);

    /// <summary>The card's interior; <see cref="EmailShell"/> supplies the frame.</summary>
    private string BuildHtml(string code, Copy copy, bool greek)
    {
        var safeCode = WebUtility.HtmlEncode(code);

        // The code is text, never an image: images are blocked by default, cannot be copied, and
        // are unreadable to a screen reader. `text-indent` compensates for the trailing letter-space.
        var content = $"""
                      <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                        <tr>
                          <td style="font-family:{EmailShell.Serif}; font-size:26px; line-height:32px; font-weight:700; color:{EmailShell.TextPrimary}; padding:0 0 16px;">
                            {WebUtility.HtmlEncode(copy.Heading)}
                          </td>
                        </tr>
                        <tr>
                          <td style="font-family:{EmailShell.Sans}; font-size:16px; line-height:26px; color:{EmailShell.TextSecondary}; padding:0 0 28px;">
                            {WebUtility.HtmlEncode(copy.Intro)}
                          </td>
                        </tr>
                        <tr>
                          <td align="center" style="background-color:{EmailShell.AccentSubtle}; border-radius:10px; padding:24px 16px;">
                            <div class="kc-code" style="font-family:'JetBrains Mono',Consolas,'Courier New',monospace; font-size:40px; line-height:48px; font-weight:700; letter-spacing:10px; text-indent:10px; color:{EmailShell.TextPrimary};">{safeCode}</div>
                          </td>
                        </tr>
                        <tr>
                          <td style="font-family:{EmailShell.Sans}; font-size:14px; line-height:22px; color:{EmailShell.TextMuted}; padding:24px 0 0;">
                            {WebUtility.HtmlEncode(copy.Expiry)}
                          </td>
                        </tr>
                        <tr>
                          <td style="font-family:{EmailShell.Sans}; font-size:14px; line-height:22px; color:{EmailShell.TextSecondary}; padding:16px 0 0;">
                            {WebUtility.HtmlEncode(copy.Ignore)}
                          </td>
                        </tr>
                      </table>
                      """;

        return EmailShell.Wrap(
            languageTag: greek ? "el" : "en",
            preheader: copy.Preheader,
            content: content,
            footer: copy.Footer,
            branding: _branding);
    }

    // Not optional — see EmailMessage.TextBody.
    private static string BuildText(string code, Copy copy) =>
        $"""
         {copy.Heading}

         {copy.CodeLine}{code}

         {copy.Expiry}
         {copy.Ignore}

         --
         {copy.Footer}
         """;

    /// <summary>
    /// Second person singular in Greek, matching the Koureia email theme's
    /// <c>messages_el.properties</c> — not Keycloak's own bundles, which use the plural.
    /// </summary>
    private sealed record Copy(
        string Heading,
        string Intro,
        string CodeLine,
        string Expiry,
        string Ignore,
        string Preheader,
        string Footer)
    {
        public static Copy Create(bool greek, int minutes)
        {
            var count = minutes.ToString(CultureInfo.InvariantCulture);

            if (greek)
            {
                // «1 λεπτό», «5 λεπτά» — Greek needs the singular, and so does English below.
                var duration = minutes == 1 ? "1 λεπτό" : $"{count} λεπτά";

                return new Copy(
                    Heading: "Επιβεβαίωσε το email σου",
                    Intro: "Γράψε τον κωδικό παρακάτω στη σελίδα επιβεβαίωσης για να ολοκληρώσεις τον λογαριασμό σου.",
                    CodeLine: "Ο κωδικός σου είναι: ",
                    Expiry: $"Ο κωδικός λειτουργεί μία φορά και λήγει σε {duration}.",
                    Ignore: "Αν δεν το ζήτησες εσύ, αγνόησε αυτό το μήνυμα. Κανένας λογαριασμός δεν δημιουργείται χωρίς αυτόν τον κωδικό.",
                    Preheader: $"Ο κωδικός λήγει σε {duration}.",
                    Footer: "Αυτόματο μήνυμα. Μην απαντήσεις σε αυτό.");
            }

            var englishDuration = minutes == 1 ? "1 minute" : $"{count} minutes";

            return new Copy(
                Heading: "Confirm your email address",
                Intro: "Enter the code below on the confirmation page to finish setting up your account.",
                CodeLine: "Your code is: ",
                Expiry: $"The code can be used once, and expires in {englishDuration}.",
                Ignore: "If you did not ask for this, ignore this message. No account is created without this code.",
                Preheader: $"The code expires in {englishDuration}.",
                Footer: "This is an automated message. Please do not reply to it.");
        }
    }
}
