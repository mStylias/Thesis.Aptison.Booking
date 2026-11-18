using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Application.Features.EmailVerification.Common.Enums;
using Aptison.Booking.Application.Features.EmailVerification.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.EmailVerification.Commands;

/// <summary>
/// Checks a submitted code and, when it matches, marks the caller's address verified.
/// </summary>
public class SubmitEmailVerificationCodeRequest : ICommand<Result<EmailVerificationResponse, HttpError>>
{
    /// <summary>From the token. Never bound from the body.</summary>
    public Guid Subject { get; set; }

    public bool EmailVerified { get; set; }

    /// <summary>Nullable because JSON can deliver <c>null</c> whatever the annotation says.</summary>
    public string? Code { get; set; }
}

/// <remarks>
/// The two refusals take different routes: already-verified is a <c>409</c> returned directly, a
/// missing code goes through <c>Validation</c> so it arrives with <c>propertyName: "code"</c>.
/// </remarks>
public class SubmitEmailVerificationCodeHandler(
    IVerificationCodeStore codeStore, IIdentityAccountsManager identityAccountsManager)
    : ICommandHandler<SubmitEmailVerificationCodeRequest, Result<EmailVerificationResponse, HttpError>>,
        IWithValidation<SubmitEmailVerificationCodeRequest, Result<string, HttpError>>
{
    public Task<Result<string, HttpError>> ValidateAsync(
        SubmitEmailVerificationCodeRequest request, CancellationToken cancellationToken)
    {
        // Reachable by a verified caller: this endpoint is exempt from the email_verified gate.
        if (request.EmailVerified)
        {
            return Task.FromResult<Result<string, HttpError>>(HttpError.Conflict(
                "Your email address is already verified.",
                errorCode: ErrorCodes.EmailVerification.AlreadyVerified));
        }

        // Shape only: a wrong code is this endpoint's most ordinary outcome, not a validation failure.
        var code = request.Code?.Trim() ?? string.Empty;

        var validation = new Validation("The verification code is invalid.")
            .Require(!string.IsNullOrEmpty(code),
                ErrorCodes.EmailVerification.CodeIncorrect,
                "A verification code is required.",
                nameof(request.Code));

        return Task.FromResult(validation.ToResult(code));
    }

    public async Task<Result<EmailVerificationResponse, HttpError>> HandleAsync(
        SubmitEmailVerificationCodeRequest request, CancellationToken cancellationToken)
    {
        // Trimmed and proven non-empty by ValidateAsync.
        var code = this.ValidationData();

        return await Result
            .TryAsync(
                () => codeStore.CheckAsync(request.Subject, code, cancellationToken),
                _ => CheckFailed())
            .BindAsync(async check => check switch
            {
                VerificationCodeCheck.Verified =>
                    await MarkVerifiedAsync(request.Subject, cancellationToken),

                // Data, not errors: the endpoint turns these into problem responses.
                VerificationCodeCheck.Incorrect incorrect => new EmailVerificationResponse(
                    EmailVerificationOutcome.CodeIncorrect,
                    incorrect.AttemptsRemaining,
                    TokenRefreshRequired: false),

                VerificationCodeCheck.AttemptsExhausted => new EmailVerificationResponse(
                    EmailVerificationOutcome.AttemptsExhausted, null, TokenRefreshRequired: false),

                VerificationCodeCheck.NotPending => new EmailVerificationResponse(
                    EmailVerificationOutcome.NoCodePending, null, TokenRefreshRequired: false),

                _ => CheckFailed()
            });
    }

    private static HttpError CheckFailed() => HttpError.Internal(
        "The verification code could not be checked. Please try again.",
        errorCode: ErrorCodes.EmailVerification.Failed);

    /// <summary>
    /// Flips <c>emailVerified</c> at the identity provider. The code is already consumed by then, so
    /// a failure here strands the person on a spent code — the remedy is a resend, and the reverse
    /// order would mark an account verified before knowing the code was right.
    /// </summary>
    private Task<Result<EmailVerificationResponse, HttpError>> MarkVerifiedAsync(
        Guid subject, CancellationToken cancellationToken) =>
        Result.TryAsync(
            async () =>
            {
                await identityAccountsManager.MarkEmailVerifiedAsync(subject, cancellationToken);
                return new EmailVerificationResponse(
                    EmailVerificationOutcome.Verified, null, TokenRefreshRequired: true);
            },
            _ => HttpError.Internal(
                "Your code was correct, but your account could not be updated. Please ask for a " +
                "new code and try again.",
                errorCode: ErrorCodes.EmailVerification.Failed));
}
