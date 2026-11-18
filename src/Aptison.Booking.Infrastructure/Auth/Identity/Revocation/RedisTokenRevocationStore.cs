using System.Globalization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Aptison.Booking.Infrastructure.Auth.Identity.Revocation;

/// <summary>
/// One Redis key per revoked subject, holding the cutoff in Unix seconds. It expires once every
/// token it refuses has expired too, so it must outlive the realm's access token lifespan (300 s).
/// </summary>
internal sealed class RedisTokenRevocationStore(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisTokenRevocationStore> logger) : ITokenRevocationStore
{
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(1);

    public async Task RevokeIssuedUntilAsync(Guid subject, DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        await multiplexer.GetDatabase().StringSetAsync(
            GetKey(subject), cutoff.ToUnixTimeSeconds(), KeyLifetime);

    public async Task<bool> IsRevokedAsync(Guid subject, DateTimeOffset issuedAt, CancellationToken cancellationToken)
    {
        RedisValue cutoff;

        try
        {
            cutoff = await multiplexer.GetDatabase().StringGetAsync(GetKey(subject));
        }
        catch (RedisException e)
        {
            // Open, not closed: refusing here would refuse every signed-in request while Redis is
            // down, and a revocation only matters for the few minutes its tokens stay valid.
            logger.LogWarning(e, "Could not read the token revocation of subject {Subject}", subject);
            return false;
        }

        return cutoff.HasValue
               && long.TryParse((string?)cutoff, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
               && issuedAt.ToUnixTimeSeconds() <= seconds;
    }

    private static RedisKey GetKey(Guid subject) => $"auth:revoked:{subject:D}";
}
