namespace Aptison.Booking.Application.Abstractions.Storage;

/// <summary>
/// The object store behind uploaded files. A key under <c>public/</c> is readable by anyone at
/// <see cref="FileStorageOptions.PublicBaseUrl"/> plus the key.
/// </summary>
public interface IFileStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>A missing key is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
