namespace Aptison.Booking.Application.Features.Users.Common.Models;

/// <summary>
/// The language wire shape. Used as an EF projection (nested inside <c>UserResponse</c> via
/// <c>QueryDto</c>), so members keep SQL-translatable source types.
/// </summary>
public record LanguageViewDto(
    Guid Id,
    string Code,
    string Name);
