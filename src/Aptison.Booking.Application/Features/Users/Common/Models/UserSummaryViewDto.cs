
namespace Aptison.Booking.Application.Features.Users.Common.Models;

/// <summary>
/// Just enough of a person to name them on someone else's screen. Not <see cref="UserResponse"/>,
/// which carries the Keycloak id and phone number — the user's own business.
/// </summary>
public record UserSummaryViewDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email);
