using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Reference;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Services.Common.Rules;

/// <summary>The language and duplicate-name rules that services and categories share.</summary>
internal static class CatalogTranslations
{
    /// <summary>The active languages by code. Every catalog name needs one translation in each.</summary>
    public static async Task<Dictionary<string, Guid>> ReadPlatformLanguagesAsync(
        IRepository<Language, BookingContext> languageRepo, CancellationToken cancellationToken)
    {
        var languages = await languageRepo
            .QuerySelect(language => new { language.Code, language.Id }, query => query.Where(language => language.IsActive))
            .ToListAsync(cancellationToken);

        return languages.ToDictionary(language => language.Code, language => language.Id, StringComparer.Ordinal);
    }

    /// <summary>Requires one translation per platform language, each language once and known.</summary>
    public static Validation RequireLanguages(
        this Validation validation,
        IReadOnlyList<string?> languageCodes,
        IReadOnlyDictionary<string, Guid> languageIdsByCode,
        string missingCode,
        string invalidCode,
        string propertyName)
    {
        var seenCodes = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < languageCodes.Count; index++)
        {
            var code = NormalizeCode(languageCodes[index]);

            validation.Require(
                languageIdsByCode.ContainsKey(code) && seenCodes.Add(code),
                invalidCode,
                "Each translation needs a different, supported language.",
                $"{propertyName}[{index}].LanguageCode");
        }

        foreach (var code in languageIdsByCode.Keys.Order(StringComparer.Ordinal).Where(code => !seenCodes.Contains(code)))
        {
            validation.Require(false, missingCode, $"A translation in '{code}' is required.", propertyName);
        }

        return validation;
    }

    public static string NormalizeCode(string? languageCode) =>
        StringUtils.TrimToNull(languageCode)?.ToLowerInvariant() ?? string.Empty;

    /// <summary>
    /// A 409 naming each wanted translation whose folded name another row already has in the same
    /// language. Null when there is no clash.
    /// </summary>
    public static HttpError? FindTakenNames(
        IReadOnlyList<TranslatedName> wantedNames,
        IEnumerable<TranslatedName> existingNames,
        string errorCode,
        string description,
        string propertyName)
    {
        var takenNames = existingNames
            .Select(existing => (existing.LanguageId, StringUtils.Fold(existing.Name)))
            .ToHashSet();

        ValidationError[] clashes = [.. wantedNames
            .Select((wanted, index) => (wanted, index))
            .Where(pair => takenNames.Contains((pair.wanted.LanguageId, StringUtils.Fold(pair.wanted.Name))))
            .Select(pair => new ValidationError(errorCode, description, $"{propertyName}[{pair.index}].Name"))];

        if (clashes.Length == 0)
        {
            return null;
        }

        var error = HttpError.Conflict(description, errorCode: errorCode);
        error.ValidationErrors = clashes;
        return error;
    }
}
