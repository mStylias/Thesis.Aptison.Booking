namespace Aptison.Booking.Application.Common.FileStorage;

public static class FileStorageUtils
{
    /// <summary>
    /// An absolute key is already a URL and is returned as-is. Any other key needs
    /// <c>Storage:PublicBaseUrl</c>, and is null without it rather than a broken link.
    /// </summary>
    public static string? BuildPublicUrl(string? storageKey, string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        if (IsAbsoluteUrl(storageKey))
        {
            return storageKey;
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return null;
        }

        return $"{publicBaseUrl.TrimEnd('/')}/{storageKey.TrimStart('/')}";
    }

    /// <summary>Seed data stores absolute URLs; those are not objects in the bucket.</summary>
    public static bool IsAbsoluteUrl(string storageKey) =>
        Uri.TryCreate(storageKey, UriKind.Absolute, out var absolute)
        && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp);
}
