using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.RateLimiting;
using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Post;

/// <summary>A test push to the caller's current browser, which matters most on an iPhone Home Screen app.</summary>
public class SendCurrentUserTestPush : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/me/push-devices/test", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Testing reminder pushes on your own browser. Every customer may turn push on, so every " +
                "customer may check it works.")
            .RequireRateLimiting(TestPushRateLimiter.PolicyName)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        SendTestPushRequest request,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Overwritten, not merged: only the caller's own devices can be tested.
        request.UserId = currentAppUser.GetRequiredUserId();

        return (await sender.SendAsync(request, cancellationToken)).ToHttpResult();
    }
}
