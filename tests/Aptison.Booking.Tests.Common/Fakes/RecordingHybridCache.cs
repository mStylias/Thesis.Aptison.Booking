using Microsoft.Extensions.Caching.Hybrid;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>A cache that holds nothing and records what was evicted, for handler unit tests.</summary>
public sealed class RecordingHybridCache : HybridCache
{
    public List<string> RemovedTags { get; } = [];

    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        factory(state, cancellationToken);

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        RemovedTags.Add(tag);
        return ValueTask.CompletedTask;
    }
}
