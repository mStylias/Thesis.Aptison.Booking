using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.EmailVerification.Commands;
using Aptison.Booking.Application.Features.EmailVerification.Common.Enums;
using Aptison.Booking.Application.Features.EmailVerification.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Auth;

/// <summary>
/// Submits a verification code and, when it is right, proves the caller's address. One of the two
/// endpoints reachable before verification.
/// <para>
/// <b>Success does not open the API to the caller's current token</b> — an already-signed token is
/// unaltered, so a client that ignores <c>tokenRefreshRequired</c> shows a verified person 403s.
/// </para>
/// </summary>
public class SubmitEmailVerificationCode : IVersionedEndpoint
{
    public string Tag => "Auth";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/email-verification/code/verify", HandleAsync)
            .AllowUnverifiedEmail(
                "Proving your address is the one thing an unverified caller must be able to do.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<EmailVerificationResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        SubmitEmailVerificationCodeBody body,
        ICurrentAuthenticatedSubject currentSubject,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var subject = currentSubject.Require();

        var result = await sender.SendAsync(
            new SubmitEmailVerificationCodeRequest
            {
                // From the token, never the body: otherwise this verifies anybody's account.
                Subject = subject.Subject,
                EmailVerified = subject.EmailVerified,
                Code = body.Code
            },
            cancellationToken);

        if (result.IsError || result.Data is null)
        {
            return result.ToHttpResult();
        }

        return result.Data.Outcome switch
        {
            EmailVerificationOutcome.Verified => TypedResults.Ok(result.Data),

            EmailVerificationOutcome.CodeIncorrect => TypedResults.Problem(new ProblemDetails
            {
                Title = ErrorCodes.EmailVerification.CodeIncorrect,
                Detail = "That code is not correct.",
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["attemptsRemaining"] = result.Data.AttemptsRemaining }
            }),

            EmailVerificationOutcome.AttemptsExhausted => TypedResults.Problem(new ProblemDetails
            {
                Title = ErrorCodes.EmailVerification.AttemptsExhausted,
                Detail = "Too many incorrect attempts. Ask for a new code.",
                Status = StatusCodes.Status400BadRequest
            }),

            _ => TypedResults.Problem(new ProblemDetails
            {
                Title = ErrorCodes.EmailVerification.NoCodePending,
                Detail = "That code has expired or was already used. Ask for a new one.",
                Status = StatusCodes.Status400BadRequest
            })
        };
    }
}

/// <summary>No subject and no address: both come from the token.</summary>
public sealed record SubmitEmailVerificationCodeBody(string Code);
