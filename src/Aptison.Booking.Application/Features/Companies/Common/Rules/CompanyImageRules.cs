namespace Aptison.Booking.Application.Features.Companies.Common.Rules;

internal static class CompanyImageRules
{
    public const string PropertyName = "File";

    private static readonly HashSet<string> AcceptedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    /// <summary>Judged by the part's declared type and length. Decoding is what proves the bytes.</summary>
    public static bool IsAcceptedUpload(string? contentType, long length, long maxBytes) =>
        length > 0 && length <= maxBytes && contentType is not null && AcceptedContentTypes.Contains(contentType);
}
