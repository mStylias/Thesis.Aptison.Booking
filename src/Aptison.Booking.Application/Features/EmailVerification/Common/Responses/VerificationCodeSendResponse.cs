namespace Aptison.Booking.Application.Features.EmailVerification.Common.Responses;

/// <summary>
/// What happened when a code was asked for. The endpoint turns <c>Sent: false</c> into a <c>429</c>,
/// and <c>DailyLimitReached</c> says which one.
/// </summary>
public record VerificationCodeSendResponse(
    bool Sent,
    bool DailyLimitReached,
    int CodeLength,
    int MaxAttempts,
    DateTimeOffset? ExpiresAt,
    int? ExpiresInSeconds,
    int ResendAvailableInSeconds);
