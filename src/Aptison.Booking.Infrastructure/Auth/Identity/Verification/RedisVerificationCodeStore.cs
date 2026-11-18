using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Aptison.Booking.Infrastructure.Auth.Identity.Verification;

/// <summary>
/// The pending verification code, in Redis: a hash of digest + attempt count expiring with the
/// code, and a marker key whose remaining TTL <em>is</em> the resend cooldown.
/// <para>
/// <b>Redis being unavailable is a failure here, unlike in the permission cache</b> — this is the
/// only place a pending code exists, so there is nothing to read through to.
/// </para>
/// </summary>
internal sealed class RedisVerificationCodeStore(
    IConnectionMultiplexer multiplexer,
    IOptions<EmailVerificationOptions> options,
    TimeProvider timeProvider) : IVerificationCodeStore
{
    private const string DigestField = "digest";
    private const string AttemptsField = "attempts";

    // One step, so parallel submits cannot each read the digest before any of them counts. Past
    // the cap it burns the code and returns no digest, and a burned key is never recreated.
    private const string CountAttemptScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then return nil end
        local attempts = redis.call('HINCRBY', KEYS[1], ARGV[1], 1)
        if attempts > tonumber(ARGV[2]) then
            redis.call('DEL', KEYS[1])
            return {attempts}
        end
        return {attempts, redis.call('HGET', KEYS[1], ARGV[3])}
        """;

    private readonly EmailVerificationOptions _options = options.Value;

    public async Task<VerificationCodeIssue> IssueAsync(Guid subject, CancellationToken cancellationToken)
    {
        var database = multiplexer.GetDatabase();
        var cooldownKey = GetCooldownKey(subject);

        // NX: with a separate check and write, two racing resends both send and only the second
        // code stays valid, so the first email's code is rejected.
        var claimed = await database.StringSetAsync(
            cooldownKey, 1, _options.ResendCooldown, When.NotExists);

        if (!claimed)
        {
            // A null TTL means the key expired between the claim and this read, so zero is correct.
            var remaining = await database.KeyTimeToLiveAsync(cooldownKey);
            return new VerificationCodeIssue.ResendTooSoon(remaining ?? TimeSpan.Zero);
        }

        var sentTodayKey = GetSentTodayKey(subject);
        var sentToday = await database.StringIncrementAsync(sentTodayKey);

        // Set on every send but kept by NX, so the window runs from the first code. A send that
        // dies between the two calls is mended by the next one, and never locks the subject out.
        await database.KeyExpireAsync(sentTodayKey, TimeSpan.FromDays(1), ExpireWhen.HasNoExpiry);

        if (sentToday > _options.MaxCodesPerDay)
        {
            var remaining = await database.KeyTimeToLiveAsync(sentTodayKey);
            return new VerificationCodeIssue.DailyLimitReached(remaining ?? TimeSpan.FromDays(1));
        }

        var code = GenerateCode();
        var codeKey = GetCodeKey(subject);

        // Attempt counter reset too: carrying it over burns a code the person never saw.
        var batch = database.CreateBatch();
        var delete = batch.KeyDeleteAsync(codeKey);
        var write = batch.HashSetAsync(codeKey,
        [
            new HashEntry(DigestField, ComputeDigest(subject, code)),
            new HashEntry(AttemptsField, 0)
        ]);
        var expire = batch.KeyExpireAsync(codeKey, _options.CodeLifetime);
        batch.Execute();

        await Task.WhenAll(delete, write, expire);

        return new VerificationCodeIssue.Issued(
            code,
            timeProvider.GetUtcNow() + _options.CodeLifetime,
            _options.ResendCooldown,
            _options.MaxAttempts);
    }

    public async Task<VerificationCodeCheck> CheckAsync(
        Guid subject, string code, CancellationToken cancellationToken)
    {
        var database = multiplexer.GetDatabase();
        var codeKey = GetCodeKey(subject);

        // Counted before the comparison: counting afterwards makes dying mid-check a free guess.
        var counted = await database.ScriptEvaluateAsync(
            CountAttemptScript, [codeKey], [AttemptsField, _options.MaxAttempts, DigestField]);

        if (counted.IsNull)
        {
            return new VerificationCodeCheck.NotPending();
        }

        var countedAttempt = (RedisResult[])counted!;
        var attempts = (long)countedAttempt[0];

        if (countedAttempt.Length < 2)
        {
            return new VerificationCodeCheck.AttemptsExhausted();
        }

        if (Matches((byte[])countedAttempt[1]!, subject, code))
        {
            // Consumed, so a code cannot be replayed by anyone who sees the email later.
            await database.KeyDeleteAsync(codeKey);
            return new VerificationCodeCheck.Verified();
        }

        if (attempts >= _options.MaxAttempts)
        {
            await database.KeyDeleteAsync(codeKey);
            return new VerificationCodeCheck.AttemptsExhausted();
        }

        return new VerificationCodeCheck.Incorrect((int)(_options.MaxAttempts - attempts));
    }

    private RedisKey GetCodeKey(Guid subject) => $"{GetPrefix()}:code:{subject:D}";

    private RedisKey GetCooldownKey(Guid subject) => $"{GetPrefix()}:resend:{subject:D}";

    private RedisKey GetSentTodayKey(Guid subject) => $"{GetPrefix()}:sent-today:{subject:D}";

    private string GetPrefix() => _options.KeyPrefix.TrimEnd(':');

    // RandomNumberGenerator, not Random: this is a credential. GetInt32 is rejection-sampled.
    private string GenerateCode()
    {
        var exclusiveUpperBound = (int)Math.Pow(10, _options.CodeLength);

        return RandomNumberGenerator
            .GetInt32(0, exclusiveUpperBound)
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(_options.CodeLength, '0');
    }

    /// <summary>
    /// Subject mixed in so one digest cannot be recognised across two accounts. This only keeps the
    /// plaintext out of MONITOR, snapshots and HGETALL — the keyspace is small enough to hash through,
    /// so the attempt cap, the daily limit and the lifetime are what actually protect the code.
    /// </summary>
    private static byte[] ComputeDigest(Guid subject, string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{subject:D}:{code}"));

    // Constant time: an early-returning comparison leaks how much of the digest matched.
    private static bool Matches(byte[] stored, Guid subject, string code) =>
        CryptographicOperations.FixedTimeEquals(stored, ComputeDigest(subject, code));
}
