using System.Net;
using System.Text.Json;
using Aptison.Booking.Application.Abstractions.Messaging;
using Lib.Net.Http.WebPush;
using Microsoft.Extensions.Logging;
using LibPushMessage = Lib.Net.Http.WebPush.PushMessage;
using PushMessage = Aptison.Booking.Application.Abstractions.Messaging.PushMessage;

namespace Aptison.Booking.Infrastructure.Reminders;

/// <summary>
/// <see cref="IPushSender"/> over Lib.Net.Http.WebPush, posting to the browser's own push service:
/// Google, Apple, Mozilla or Microsoft. The payload is encrypted for the browser, so they see the
/// endpoint and never the text.
/// </summary>
internal sealed class WebPushSender(HttpClient httpClient, VapidKeys vapidKeys, ILogger<WebPushSender> logger)
    : IPushSender
{
    public const string HttpClientName = "web-push";

    /// <summary>The longest TTL push services accept: four weeks.</summary>
    private const int MaxTimeToLiveSeconds = 2_419_200;

    public async Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        var client = new PushServiceClient(httpClient)
        {
            DefaultAuthentication = vapidKeys.Authentication,

            // Waiting out a Retry-After inside the call would hold an outbox dispatch; the outbox retries instead.
            AutoRetryAfter = false
        };

        var subscription = new PushSubscription { Endpoint = message.Subscription.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, message.Subscription.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, message.Subscription.Auth);

        var push = new LibPushMessage(JsonSerializer.Serialize(message.Content, JsonSerializerOptions.Web))
        {
            TimeToLive = (int)Math.Clamp(message.TimeToLive.TotalSeconds, 0, MaxTimeToLiveSeconds),
            Urgency = message.Urgency == PushUrgency.High ? PushMessageUrgency.High : PushMessageUrgency.Normal,
            Topic = message.Topic
        };

        try
        {
            await client.RequestPushMessageDeliveryAsync(subscription, push, cancellationToken);

            return PushSendResult.Delivered;
        }
        catch (PushServiceClientException exception)
            when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushSendResult.SubscriptionGone;
        }
        catch (PushServiceClientException exception)
        {
            // Never the endpoint: it identifies the person's browser.
            logger.LogWarning(
                "A push service refused a push with {StatusCode}: {Body}", (int)exception.StatusCode, exception.Body);
            throw;
        }
    }
}
