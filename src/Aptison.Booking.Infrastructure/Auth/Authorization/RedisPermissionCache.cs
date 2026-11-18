using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// Two Redis structures: <c>{prefix}:roles</c>, one hash field per role holding its permission
/// codes, and <c>{prefix}:access:{id}</c>, one string per user holding their <see cref="UserAccessEntry"/>.
/// <para>
/// Fills are guarded by the <c>{prefix}:v</c> counter because nothing here expires: a reader
/// overtaken by an invalidation would otherwise write a stale entry that is served forever, and
/// revocation is the property this design exists to provide. The single counter aborts any fill in
/// flight on any invalidation — an occasional needless rebuild, no key per entry.
/// </para>
/// </summary>
internal sealed class RedisPermissionCache : IPermissionCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisPermissionCache> _logger;
    private readonly AuthorizationCacheOptions _options;

    private readonly RedisKey _versionKey;
    private readonly RedisKey _rolesKey;
    private readonly string _accessKeyPrefix;

    public RedisPermissionCache(
        IConnectionMultiplexer multiplexer,
        IOptions<AuthorizationCacheOptions> options,
        ILogger<RedisPermissionCache> logger)
    {
        _multiplexer = multiplexer;
        _logger = logger;
        _options = options.Value;

        var prefix = _options.KeyPrefix.TrimEnd(':');
        _versionKey = $"{prefix}:v";
        _rolesKey = $"{prefix}:roles";
        _accessKeyPrefix = $"{prefix}:access:";
    }

    public bool IsAvailable => _multiplexer.IsConnected;

    // ---------- Reads ---------- //

    public async Task<UserAccessEntry?> GetUserAccessAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await GetDatabase().StringGetAsync(GetAccessKey(userId));

            if (payload.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<UserAccessEntry>((string)payload!, SerializerOptions);
        }
        catch (Exception e) when (IsCacheFailure(e))
        {
            LogDegraded(e, "reading access for user {UserId}", userId);
            return null;
        }
        catch (JsonException e)
        {
            // Left in place it would fail identically on every request, so drop it.
            _logger.LogError(e, "Discarding unreadable cached access for user {UserId}", userId);
            await InvalidateUserAsync(userId, cancellationToken);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, string[]>> GetRolePermissionsAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return new Dictionary<Guid, string[]>();
        }

        try
        {
            // Materialised once: the reply comes back in the order asked, so re-enumerating the
            // caller's collection could pair the wrong codes with the wrong role.
            Guid[] ordered = [.. roleIds];
            var fields = Array.ConvertAll(ordered, roleId => (RedisValue)roleId.ToString("D"));
            var cachedValues = await GetDatabase().HashGetAsync(_rolesKey, fields);

            var resolved = new Dictionary<Guid, string[]>(ordered.Length);

            for (var index = 0; index < ordered.Length; index++)
            {
                var value = cachedValues[index];

                // Absent means "unknown role, go and look"; an empty array means "grants nothing".
                // Collapsing them rebuilds the whole map on every permissionless role.
                if (value.IsNullOrEmpty)
                {
                    continue;
                }

                resolved[ordered[index]] =
                    JsonSerializer.Deserialize<string[]>((string)value!, SerializerOptions) ?? [];
            }

            return resolved;
        }
        catch (Exception e) when (IsCacheFailure(e))
        {
            LogDegraded(e, "reading the role map");
            return new Dictionary<Guid, string[]>();
        }
        catch (JsonException e)
        {
            _logger.LogError(e, "Discarding the unreadable cached role map");
            await RunSafelyAsync(() => GetDatabase().KeyDeleteAsync(_rolesKey), "discarding the role map");
            return new Dictionary<Guid, string[]>();
        }
    }

    // ---------- Fills ---------- //

    public async Task<long> BeginFillAsync(CancellationToken cancellationToken)
    {
        try
        {
            var raw = await GetDatabase().StringGetAsync(_versionKey);

            // Absent means nothing has ever been invalidated; zero stands for it, and the fill
            // condition below turns that into "the key must still be absent".
            return raw.IsNullOrEmpty || !raw.TryParse(out long version) ? 0 : version;
        }
        catch (Exception e) when (IsCacheFailure(e))
        {
            LogDegraded(e, "reading the fill token");

            // Never a real token, so the fill it guards is skipped rather than written blind.
            return -1;
        }
    }

    public Task FillUserAccessAsync(
        Guid userId, UserAccessEntry access, long fillToken, CancellationToken cancellationToken) =>
        FillAsync(
            fillToken,
            // When.Always selects the nullable-TimeSpan overload, where null means no expiry; the
            // newer Expiration overload cannot express that.
            transaction => transaction.StringSetAsync(
                GetAccessKey(userId),
                JsonSerializer.Serialize(access, SerializerOptions),
                _options.UserEntryExpiry,
                When.Always),
            "access for user {UserId}",
            userId);

    public Task FillRolePermissionsAsync(
        IReadOnlyDictionary<Guid, string[]> permissionsByRole, long fillToken, CancellationToken cancellationToken)
    {
        var hashEntries = permissionsByRole
            .Select(pair => new HashEntry(
                pair.Key.ToString("D"),
                JsonSerializer.Serialize(pair.Value, SerializerOptions)))
            .ToArray();

        return FillAsync(
            fillToken,
            transaction =>
            {
                // Replaced wholesale: HSET alone leaves a deleted role's field behind forever.
                _ = transaction.KeyDeleteAsync(_rolesKey);
                return hashEntries.Length == 0
                    ? Task.CompletedTask
                    : transaction.HashSetAsync(_rolesKey, hashEntries);
            },
            "the role map ({RoleCount} roles)",
            hashEntries.Length);
    }

    /// <summary>
    /// Applies <paramref name="write"/> only if the guard counter still reads
    /// <paramref name="fillToken"/>. See the type-level note.
    /// </summary>
    private async Task FillAsync(
        long fillToken, Func<ITransaction, Task> write, string what, params object?[] args)
    {
        if (fillToken < 0)
        {
            return;
        }

        try
        {
            var transaction = GetDatabase().CreateTransaction();

            transaction.AddCondition(fillToken == 0
                ? Condition.KeyNotExists(_versionKey)
                : Condition.StringEqual(_versionKey, fillToken));

            // Not awaited: inside a transaction these complete only when Execute does.
            _ = write(transaction);

            if (!await transaction.ExecuteAsync())
            {
                _logger.LogDebug(
                    "Skipped caching " + what + ": invalidated while it was being read", args);
            }
        }
        catch (Exception e) when (IsCacheFailure(e))
        {
            LogDegraded(e, "caching " + what, args);
        }
    }

    // ---------- Invalidation ---------- //

    public Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        RunSafelyAsync(
            () =>
            {
                var batch = GetDatabase().CreateBatch();
                var bump = batch.StringIncrementAsync(_versionKey);
                var drop = batch.HashDeleteAsync(_rolesKey, roleId.ToString("D"));
                batch.Execute();
                return Task.WhenAll(bump, drop);
            },
            "invalidating role {RoleId}", roleId);

    public Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        RunSafelyAsync(
            () =>
            {
                var batch = GetDatabase().CreateBatch();
                var bump = batch.StringIncrementAsync(_versionKey);
                var drop = batch.KeyDeleteAsync(GetAccessKey(userId));
                batch.Execute();
                return Task.WhenAll(bump, drop);
            },
            "invalidating user {UserId}", userId);

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        // SCAN, not KEYS: this runs against a live server.
        await RunSafelyAsync(
            async () =>
            {
                var database = GetDatabase();
                await database.StringIncrementAsync(_versionKey);
                await database.KeyDeleteAsync(_rolesKey);

                foreach (var endpoint in _multiplexer.GetEndPoints())
                {
                    var server = _multiplexer.GetServer(endpoint);

                    if (!server.IsConnected || server.IsReplica)
                    {
                        continue;
                    }

                    await foreach (var key in server
                                       .KeysAsync(database.Database, $"{_accessKeyPrefix}*")
                                       .WithCancellation(cancellationToken))
                    {
                        await database.KeyDeleteAsync(key);
                    }
                }

                // Inside the guarded action, so a half-failed flush does not report success.
                _logger.LogInformation(
                    "Flushed the permission cache. Every user's access rebuilds from Postgres on " +
                    "their next request");
            },
            "invalidating every cached entry");
    }

    // ---------- Plumbing ---------- //

    private IDatabase GetDatabase() => _multiplexer.GetDatabase();

    private RedisKey GetAccessKey(Guid userId) => _accessKeyPrefix + userId.ToString("D");

    // Callers fall back to Postgres, so a cache failure must never propagate as a 500.
    private static bool IsCacheFailure(Exception exception) =>
        exception is RedisException or TimeoutException or ObjectDisposedException;

    private async Task RunSafelyAsync(Func<Task> action, string what, params object?[] args)
    {
        try
        {
            await action();
        }
        catch (Exception e) when (IsCacheFailure(e))
        {
            LogDegraded(e, what, args);
        }
    }

    private void LogDegraded(Exception exception, string what, params object?[] args) =>
        _logger.LogWarning(
            exception,
            "Permission cache unavailable while " + what + ". Serving from Postgres instead",
            args);
}
