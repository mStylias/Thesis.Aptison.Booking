using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>
/// The caller's own profile. Reaching this endpoint means the row exists — the middleware
/// provisions it — so there is no "authenticated but unknown" response to handle.
/// </summary>
public class GetCurrentUser : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reading your own profile. No role should be able to withhold it, and a permission " +
                "code for it would be one every role had to carry.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<UserResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // From the token, never from the caller.
        var result = await sender.SendAsync(
            new GetUserRequest { Id = currentAppUser.GetRequiredUserId() }, cancellationToken);

        return result.ToHttpResult();
    }
}
