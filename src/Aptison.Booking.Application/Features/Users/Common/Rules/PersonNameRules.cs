using System.Text.RegularExpressions;

namespace Aptison.Booking.Application.Features.Users.Common.Rules;

/// <summary>
/// The realm's user-profile limits on <c>firstName</c> and <c>lastName</c>. A name past them is
/// saved here but refused by Keycloak, so the copy never lands and the sign-in pages keep the old one.
/// </summary>
internal static partial class PersonNameRules
{
    public const int MaxLength = 64;

    public static bool IsValid(string? name)
    {
        var trimmed = name?.Trim();

        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaxLength && AllowedCharacters().IsMatch(trimmed);
    }

    // Keycloak's person-name-prohibited-characters validator.
    [GeneratedRegex(@"^[^<>&""\v$%!#?§;*~/\\|^=\[\]{}()\p{Cc}]+$")]
    private static partial Regex AllowedCharacters();
}
