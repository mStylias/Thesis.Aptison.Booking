namespace Aptison.Booking.Application.Abstractions.Storage;

/// <summary>The part of the <c>Storage</c> section Application reads. The bucket credentials stay in Infrastructure.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Prefixed to a storage key to make its public URL. While empty, only keys that are already
    /// absolute URLs are returned and the rest are left out rather than sent broken.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
