using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.EmailVerification.Commands;
using Aptison.Booking.Application.Features.EmailVerification.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mapper.Core;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Auth;

/// <summary>
/// Sends — or resends — a verification code to the caller's own address. One of two endpoints
/// reachable before the address is proven, and it takes no body: subject and address come off the
/// token, so it cannot be aimed at a stranger's inbox.
/// </summary>
public class SendEmailVerificationCode : IVersionedEndpoint
{
    public string Tag => "Auth";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/email-verification/code", HandleAsync)
            .AllowUnverifiedEmail(
                "Sending yourself a verification code is what an unverified caller exists to do. " +
                "Requiring a verified address to reach it would be a closed loop.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<VerificationCodeSendResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        ICurrentAuthenticatedSubject currentSubject,
        IMapper mapper,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Every field from the token, through the registered map.
        var request = mapper.Map<SendEmailVerificationCodeRequest>(currentSubject.Require());

        var result = await sender.SendAsync(request, cancellationToken);

        if (result.IsError || result.Data is null)
        {
            return result.ToHttpResult();
        }

        if (result.Data.Sent)
        {
            return TypedResults.Ok(result.Data);
        }

        // Data out of the handler; it becomes a 429 here, where HTTP shaping belongs.
        return TypedResults.Problem(new ProblemDetails
        {
            Title = result.Data.DailyLimitReached
                ? ErrorCodes.EmailVerification.DailyLimitReached
                : ErrorCodes.EmailVerification.ResendTooSoon,
            Detail = result.Data.DailyLimitReached
                ? "Too many codes were sent to this address today. Try again later."
                : "A code was sent recently. The one you already have is still valid.",
            Status = StatusCodes.Status429TooManyRequests,
            Extensions = { ["retryAfterSeconds"] = result.Data.ResendAvailableInSeconds }
        });
    }
}
