using Aptison.Booking.Application.Abstractions.Storage;

namespace Aptison.Booking.Infrastructure.Storage;

/// <summary>
/// The bucket side of the <c>Storage</c> section; <see cref="FileStorageOptions"/> reads the public
/// URL from the same section. Any S3-compatible provider: RustFS in development.
/// </summary>
public sealed class S3StorageOptions
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";

    /// <summary>RustFS needs <c>host/bucket/key</c>; virtual-hosted buckets need a DNS entry per bucket.</summary>
    public bool ForcePathStyle { get; set; } = true;
}
