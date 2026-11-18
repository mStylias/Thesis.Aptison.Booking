using Microsoft.AspNetCore.WebUtilities;

namespace Aptison.Booking.Application.Features.Reminders.Common.Rules;

/// <summary>What a browser's <c>PushSubscription</c> looks like, so a malformed one is refused here rather than at every send.</summary>
public static class PushSubscriptionRules
{
    public const int MaxEndpointLength = 2_048;
    public const int MaxLabelLength = 200;

    /// <summary>An uncompressed P-256 point.</summary>
    private const int P256dhLength = 65;
    private const int AuthLength = 16;

    /// <summary>Every push service is HTTPS; anything else would make the API post to an arbitrary host in clear.</summary>
    public static bool IsValidEndpoint(string? endpoint) =>
        endpoint is { Length: > 0 and <= MaxEndpointLength }
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !uri.IsLoopback;

    public static bool IsValidP256dh(string? key) => HasDecodedLength(key, P256dhLength);

    public static bool IsValidAuth(string? secret) => HasDecodedLength(secret, AuthLength);

    private static bool HasDecodedLength(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        try
        {
            return WebEncoders.Base64UrlDecode(value.Trim().TrimEnd('=')).Length == length;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
