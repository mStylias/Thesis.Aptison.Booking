using Aptison.Booking.Application.Common.StringOperations;

namespace Aptison.Booking.Application.Features.Companies.Common;

/// <summary>
/// Folds a search term the way the database folded <c>company_translations.name_normalized</c>, and
/// wraps it as a <c>LIKE</c> pattern.
/// </summary>
internal static class CompanySearchTerm
{
    /// <summary>The escape character the <c>LIKE</c> must declare; see <see cref="ToPattern"/>.</summary>
    public const string LikeEscape = "\\";

    /// <summary>
    /// Null when there is nothing to search for. Otherwise a <c>%term%</c> pattern over the folded
    /// term, with the wildcards a caller typed neutralized.
    /// </summary>
    public static string? ToPattern(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        // StringUtils.Fold matches the database's fold(text), lower(unaccent(...)), on Greek and
        // Latin, except that neither folds final sigma: a term typed with 'ς' will not match 'σ'.
        return $"%{EscapeWildcards(StringUtils.Fold(term))}%";
    }

    // A '%' typed into the search box is a literal, not "match everything".
    private static string EscapeWildcards(string term) => term
        .Replace(LikeEscape, LikeEscape + LikeEscape)
        .Replace("%", LikeEscape + "%")
        .Replace("_", LikeEscape + "_");
}
