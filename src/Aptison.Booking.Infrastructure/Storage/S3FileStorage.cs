using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Aptison.Booking.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Infrastructure.Storage;

internal sealed class S3FileStorage : IFileStorage, IDisposable
{
    // Every key is new per upload, so an object never changes under its URL.
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private readonly S3StorageOptions _options;
    private readonly Lazy<AmazonS3Client> _client;

    public S3FileStorage(IOptions<S3StorageOptions> options)
    {
        _options = options.Value;
        _client = new Lazy<AmazonS3Client>(CreateClient);
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = GetBucket(),
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        request.Headers.CacheControl = ImmutableCacheControl;

        await _client.Value.PutObjectAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await _client.Value.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = GetBucket(), Key = key }, cancellationToken);

    public void Dispose()
    {
        if (_client.IsValueCreated)
        {
            _client.Value.Dispose();
        }
    }

    private string GetBucket() =>
        string.IsNullOrWhiteSpace(_options.Bucket)
            ? throw new InvalidOperationException("Storage:Bucket is not configured, so no file can be stored.")
            : _options.Bucket;

    private AmazonS3Client CreateClient() =>
        new(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                ForcePathStyle = _options.ForcePathStyle,
                AuthenticationRegion = _options.Region,
                // The SDK's default trailing checksums are not understood by every S3-compatible store.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });
}
