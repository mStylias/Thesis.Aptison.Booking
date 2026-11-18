using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Application.Features.EmailVerification.Common.Responses;
using Aptison.Booking.Application.Features.EmailVerification.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.EmailVerification.Commands;

/// <summary>
/// Issues a verification code for the caller's own address and stages the email carrying it. Every
/// field comes off the token — no address parameter, so this cannot be pointed at a stranger.
/// </summary>
public class SendEmailVerificationCodeRequest : ICommand<Result<VerificationCodeSendResponse, HttpError>>
{
    public Guid Subject { get; set; }

    public string? Email { get; set; }

    /// <summary>Answered rather than assumed: this endpoint is exempt from the verified gate.</summary>
    public bool EmailVerified { get; set; }

    public string? Locale { get; set; }
}

/// <remarks>
/// Validation is data-carrying: it returns the trimmed address, read back with
/// <c>this.ValidationData()</c>. Neither refusal is a <c>400</c> — the pipeline passes a validation
/// <c>HttpError</c> through unchanged.
/// </remarks>
public class SendEmailVerificationCodeHandler(
    IVerificationCodeStore codeStore,
    IOutbox<BookingContext> outbox,
    IUnitOfWork<BookingContext> unitOfWork)
    : ICommandHandler<SendEmailVerificationCodeRequest, Result<VerificationCodeSendResponse, HttpError>>,
        IWithValidation<SendEmailVerificationCodeRequest, Result<string, HttpError>>
{
    public Task<Result<string, HttpError>> ValidateAsync(
        SendEmailVerificationCodeRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Result
            .Ok<SendEmailVerificationCodeRequest, HttpError>(request)
            .Ensure(
                candidate => !candidate.EmailVerified,
                _ => HttpError.Conflict(
                    "Your email address is already verified.",
                    errorCode: ErrorCodes.EmailVerification.AlreadyVerified))
            .Map(candidate => string.IsNullOrWhiteSpace(candidate.Email)
                ? null
                : candidate.Email.Trim())
            .EnsureNotNull(() => HttpError.Internal(
                "Your account has no email address, so no code can be sent.",
                errorCode: ErrorCodes.EmailVerification.EmailMissing)));

    public async Task<Result<VerificationCodeSendResponse, HttpError>> HandleAsync(
        SendEmailVerificationCodeRequest request, CancellationToken cancellationToken)
    {
        var email = this.ValidationData();

        return await Result
            .TryAsync(
                () => codeStore.IssueAsync(request.Subject, cancellationToken),
                _ => IssueFailed())
            .BindAsync(async issue => issue switch
            {
                VerificationCodeIssue.Issued code =>
                    await StageEmailAsync(request, email, code, cancellationToken),

                // Data, not an error; the endpoint turns this into a 429.
                VerificationCodeIssue.ResendTooSoon tooSoon => new VerificationCodeSendResponse(
                    Sent: false,
                    DailyLimitReached: false,
                    CodeLength: 0,
                    MaxAttempts: 0,
                    ExpiresAt: null,
                    ExpiresInSeconds: null,
                    ResendAvailableInSeconds: (int)Math.Ceiling(tooSoon.RetryAfter.TotalSeconds)),

                VerificationCodeIssue.DailyLimitReached limit => new VerificationCodeSendResponse(
                    Sent: false,
                    DailyLimitReached: true,
                    CodeLength: 0,
                    MaxAttempts: 0,
                    ExpiresAt: null,
                    ExpiresInSeconds: null,
                    ResendAvailableInSeconds: (int)Math.Ceiling(limit.RetryAfter.TotalSeconds)),

                _ => IssueFailed()
            });
    }

    private static HttpError IssueFailed() => HttpError.Internal(
        "The verification code could not be issued. Please try again.",
        errorCode: ErrorCodes.EmailVerification.Failed);

    /// <summary>
    /// Stages the email in the outbox and commits it. The code is issued <em>before</em> this, so a
    /// crash between them leaves a valid code with no email, which a resend clears; the reverse
    /// would permanently reject a correct code.
    /// <para>
    /// <see cref="OutboxMessage{TContext}"/> carries no audit or ownership columns, which is what
    /// lets an unprovisioned caller write one at all.
    /// </para>
    /// </summary>
    private async Task<Result<VerificationCodeSendResponse, HttpError>> StageEmailAsync(
        SendEmailVerificationCodeRequest request,
        string email,
        VerificationCodeIssue.Issued issued,
        CancellationToken cancellationToken)
    {
        return await Result.TryAsync(
            async () =>
            {
                await outbox.EnqueueAsync(
                    new EmailVerificationCodeIssued(
                        Email: email,
                        Code: issued.Code,
                        ExpiresAt: issued.ExpiresAt,
                        LanguageCode: request.Locale ?? string.Empty),
                    cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return new VerificationCodeSendResponse(
                    Sent: true,
                    DailyLimitReached: false,
                    CodeLength: issued.Code.Length,
                    MaxAttempts: issued.AttemptsAllowed,
                    ExpiresAt: issued.ExpiresAt,
                    ExpiresInSeconds: (int)Math.Ceiling((issued.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds),
                    ResendAvailableInSeconds: (int)Math.Ceiling(issued.ResendAvailableIn.TotalSeconds));
            },
            _ => HttpError.Internal(
                "The verification email could not be queued. Please try again.",
                errorCode: ErrorCodes.EmailVerification.Failed));
    }
}
