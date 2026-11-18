using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Infrastructure.Reminders;

/// <summary>One <see cref="VapidAuthentication"/> for the process, built on first use so an environment without keys still starts.</summary>
internal sealed class VapidKeys(IOptions<WebPushOptions> options) : IDisposable
{
    private readonly Lazy<VapidAuthentication> _authentication = new(() =>
        new VapidAuthentication(options.Value.PublicKey.Trim(), options.Value.PrivateKey.Trim())
        {
            Subject = options.Value.Subject.Trim()
        });

    public VapidAuthentication Authentication => _authentication.Value;

    /// <summary>A P-256 public point is 65 bytes and its private scalar 32, which is what a generator emits.</summary>
    public static bool AreWellFormed(WebPushOptions options) =>
        HasDecodedLength(options.PublicKey, 65)
        && HasDecodedLength(options.PrivateKey, 32)
        && (options.Subject.StartsWith("mailto:", StringComparison.Ordinal)
            || options.Subject.StartsWith("https://", StringComparison.Ordinal));

    public void Dispose()
    {
        if (_authentication.IsValueCreated)
        {
            _authentication.Value.Dispose();
        }
    }

    private static bool HasDecodedLength(string value, int length)
    {
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
