using Aptison.Booking.Application.Abstractions.Storage;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>An in-memory bucket. Records what was stored and deleted.</summary>
public sealed class FakeFileStorage : IFileStorage
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (byte[] Content, string ContentType)> _objects = [];
    private readonly List<string> _deletedKeys = [];

    public IReadOnlyDictionary<string, (byte[] Content, string ContentType)> Objects
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, (byte[] Content, string ContentType)>(_objects);
            }
        }
    }

    public IReadOnlyList<string> DeletedKeys
    {
        get
        {
            lock (_gate)
            {
                return _deletedKeys.ToList();
            }
        }
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        lock (_gate)
        {
            _objects[key] = (buffer.ToArray(), contentType);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _objects.Remove(key);
            _deletedKeys.Add(key);
        }

        return Task.CompletedTask;
    }
}
