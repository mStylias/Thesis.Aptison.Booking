using System.Text.Json;
using Aptison.Booking.Application.Abstractions.Messaging;

namespace Aptison.Booking.Application.Features.Reminders.Common;

/// <summary>
/// <c>user_devices.push_data</c> for the web: <c>{ endpoint, keys: { p256dh, auth } }</c>, the shape
/// of a browser's <c>PushSubscription.toJSON()</c>. <c>ux_user_devices_web_endpoint</c> indexes the endpoint.
/// </summary>
internal static class PushDeviceData
{
    public const string EndpointProperty = "endpoint";

    public static JsonDocument Create(PushSubscriptionKeys subscription) =>
        JsonSerializer.SerializeToDocument(new Dictionary<string, object>
        {
            [EndpointProperty] = subscription.Endpoint,
            ["keys"] = new Dictionary<string, string> { ["p256dh"] = subscription.P256dh, ["auth"] = subscription.Auth }
        });

    /// <summary>Null for a row that is not a usable web subscription.</summary>
    public static PushSubscriptionKeys? ReadSubscription(JsonDocument pushData)
    {
        var root = pushData.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(EndpointProperty, out var endpoint)
            || !root.TryGetProperty("keys", out var keys)
            || keys.ValueKind != JsonValueKind.Object
            || !keys.TryGetProperty("p256dh", out var p256dh)
            || !keys.TryGetProperty("auth", out var auth))
        {
            return null;
        }

        return endpoint.GetString() is { Length: > 0 } url
               && p256dh.GetString() is { Length: > 0 } publicKey
               && auth.GetString() is { Length: > 0 } secret
            ? new PushSubscriptionKeys(url, publicKey, secret)
            : null;
    }
}
