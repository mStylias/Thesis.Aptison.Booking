using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Aptison.Booking.Application.Features.Reminders.Common;

/// <summary>
/// The unsubscribe link's token: a user id and the "email reminders" purpose under an HMAC. It does
/// not expire and can do nothing else; changing the secret breaks every link already sent.
/// </summary>
public static class ReminderUnsubscribeToken
{
    private const byte Version = 1;
    private const byte EmailRemindersPurpose = 1;
    private const int PayloadLength = 2 + 16;
    private const int MacLength = 16;

    public static string Create(Guid userId, string secret)
    {
        var token = new byte[PayloadLength + MacLength];

        token[0] = Version;
        token[1] = EmailRemindersPurpose;
        userId.TryWriteBytes(token.AsSpan(2, 16));
        ComputeMac(token.AsSpan(0, PayloadLength), secret).CopyTo(token.AsSpan(PayloadLength));

        return WebEncoders.Base64UrlEncode(token);
    }

    /// <summary>Null for anything this API did not sign.</summary>
    public static Guid? Read(string? token, string secret)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
        {
            return null;
        }

        byte[] bytes;

        try
        {
            bytes = WebEncoders.Base64UrlDecode(token.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length != PayloadLength + MacLength
            || bytes[0] != Version
            || bytes[1] != EmailRemindersPurpose
            || !CryptographicOperations.FixedTimeEquals(
                ComputeMac(bytes.AsSpan(0, PayloadLength), secret), bytes.AsSpan(PayloadLength)))
        {
            return null;
        }

        return new Guid(bytes.AsSpan(2, 16));
    }

    private static byte[] ComputeMac(ReadOnlySpan<byte> payload, string secret) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload)[..MacLength];
}
