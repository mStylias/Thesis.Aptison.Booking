using Aptison.Booking.Application.Features.Users.Common.Models;


namespace Aptison.Booking.Application.Features.Users.Common.Responses;

public record UserResponse(
    Guid Id,
    Guid? KeycloakId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    LanguageViewDto Language,
    bool EmailRemindersEnabled);
