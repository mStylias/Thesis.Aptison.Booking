using System.Text;
using Aptison.Booking.Application.Abstractions.Messaging;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Aptison.Booking.Infrastructure.Messaging;

/// <summary>
/// <see cref="IEmailSender"/> over SMTP via MailKit. A connection per message: the send rate is one
/// per registration, so the connection cost is invisible and pooling bugs would not be.
/// </summary>
internal sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            // Throwing puts the message on the outbox retry path, where it survives the fix.
            throw new InvalidOperationException(
                "Smtp:Host is not configured, so no mail can be sent. Verification codes are the " +
                "only way a registered user reaches the rest of the API.");
        }

        var mime = Compose(message);

        using var client = new SmtpClient { Timeout = (int)_options.Timeout.TotalMilliseconds };

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        // Never the subject or body: both can carry a verification code.
        logger.LogInformation("Sent an email to {Recipient}", message.To);
    }

    private MimeMessage Compose(EmailMessage message)
    {
        var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        if (message.Unsubscribe is { } unsubscribe)
        {
            mime.Headers.Add("List-Unsubscribe", $"<{unsubscribe.Url}>");

            if (unsubscribe.OneClick)
            {
                // RFC 8058: Gmail and Apple Mail then post to the URL themselves, with no page in between.
                mime.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
            }
        }

        // Both halves: an HTML-only message scores badly with spam filters.
        var alternative = new MultipartAlternative
        {
            new TextPart(MimeKit.Text.TextFormat.Plain) { Text = message.TextBody },
            new TextPart(MimeKit.Text.TextFormat.Html) { Text = message.HtmlBody }
        };

        if (message.Calendar is not { } calendar)
        {
            mime.Body = alternative;
            return mime;
        }

        // Google's own layout: the inline part is what Gmail, Outlook and iCloud turn into an event
        // card, and the attachment is what every other client offers to open.
        alternative.Add(CreateCalendarPart(calendar, attachment: false));

        mime.Body = new Multipart("mixed") { alternative, CreateCalendarPart(calendar, attachment: true) };

        return mime;
    }

    private static MimePart CreateCalendarPart(EmailCalendarPart calendar, bool attachment)
    {
        var part = new MimePart(attachment ? "application" : "text", attachment ? "ics" : "calendar")
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes(calendar.Content))),
            ContentTransferEncoding = ContentEncoding.Base64
        };

        part.ContentType.Charset = "utf-8";
        part.ContentType.Parameters.Add("method", calendar.Method);

        if (attachment)
        {
            part.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment);
            part.FileName = "invite.ics";
        }

        return part;
    }
}
