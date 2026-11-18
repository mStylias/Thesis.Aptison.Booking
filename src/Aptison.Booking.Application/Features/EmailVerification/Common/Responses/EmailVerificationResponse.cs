using Aptison.Booking.Application.Features.EmailVerification.Common.Enums;

namespace Aptison.Booking.Application.Features.EmailVerification.Common.Responses;

/// <summary>
/// <c>TokenRefreshRequired</c> is always true on success: verifying does not alter an already-issued
/// token, so a client that ignores it shows a verified person a wall of refusals.
/// </summary>
public record EmailVerificationResponse(
    EmailVerificationOutcome Outcome,
    int? AttemptsRemaining,
    bool TokenRefreshRequired);
