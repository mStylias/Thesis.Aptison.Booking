using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Common.Rules;

internal static class CompanyPhotoRules
{
    public const int MaxPhotos = 20;
    public const int MaxSidePixels = 2048;
    public const int MinShorterSidePixels = 600;
    public const int MaxAltTextLength = 300;

    public static readonly string[] AltTextLanguageCodes = ["el", "en"];

    /// <summary>Alt text by language code; a null text clears that language. A language left out is not touched.</summary>
    public static Result<Dictionary<string, string?>, HttpError> ParseAltTexts(
        IReadOnlyList<CompanyPhotoAltTextAddEditDto> altTexts, string propertyName)
    {
        var validation = new Validation("The alt text is invalid.");
        var altTextsByLanguageCodes = new Dictionary<string, string?>(StringComparer.Ordinal);

        for (var index = 0; index < altTexts.Count; index++)
        {
            var languageCode = StringUtils.TrimToNull(altTexts[index].LanguageCode)?.ToLowerInvariant();
            var altText = StringUtils.TrimToNull(altTexts[index].AltText);

            var isKnownLanguage = languageCode is not null && AltTextLanguageCodes.Contains(languageCode);
            var isFirstForLanguage = isKnownLanguage && !altTextsByLanguageCodes.ContainsKey(languageCode!);

            validation
                .Require(isKnownLanguage, ErrorCodes.Companies.InvalidPhotoAltText,
                    $"The language must be one of {string.Join(", ", AltTextLanguageCodes)}.",
                    $"{propertyName}[{index}].languageCode")
                .Require(!isKnownLanguage || isFirstForLanguage, ErrorCodes.Companies.InvalidPhotoAltText,
                    "Each language may appear once.", $"{propertyName}[{index}].languageCode")
                .Require(altText is null || altText.Length <= MaxAltTextLength, ErrorCodes.Companies.InvalidPhotoAltText,
                    $"The alt text must be at most {MaxAltTextLength} characters.", $"{propertyName}[{index}].altText");

            if (isFirstForLanguage)
            {
                altTextsByLanguageCodes[languageCode!] = altText;
            }
        }

        return validation.ToResult(altTextsByLanguageCodes);
    }

    public static HttpError CreateNotFoundError() =>
        HttpError.NotFound("The photo was not found.", errorCode: ErrorCodes.Companies.PhotoNotFound);
}
