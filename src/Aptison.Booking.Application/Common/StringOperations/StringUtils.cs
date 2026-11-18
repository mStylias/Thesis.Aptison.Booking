using System.Globalization;
using System.Text;

namespace Aptison.Booking.Application.Common.StringOperations;

/// <summary>
/// String shaping shared across features. Every case fold here is invariant: the current culture
/// would make the same input fold differently on a Turkish server than on a Greek one.
/// </summary>
public static class StringUtils
{
    public static string ToLowerCase(string value) => value.ToLowerInvariant();

    public static string[] ToLowerCase(IReadOnlyList<string> values) =>
        [.. values.Select(ToLowerCase)];

    /// <summary>Trimmed, or null when there was nothing but whitespace to trim.</summary>
    public static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Trimmed, lowercased and stripped of accents. Compare the results ordinally: the production
    /// image runs in invariant globalization mode, where a culture-aware comparison cannot be
    /// relied on to ignore accents.
    /// </summary>
    public static string Fold(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(character);
            }
        }

        return folded.ToString().Normalize(NormalizationForm.FormC);
    }
}
