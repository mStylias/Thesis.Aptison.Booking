using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.UserResources.Commands;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Post;

/// <summary>
/// Stores a resource for the caller. <c>value</c> is any JSON the client wants; the platform never
/// reads it.
/// </summary>
public class CreateCurrentUserResource : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/me/resources", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Storing your own resource. Gating it would leave a signed-in user unable to save " +
                "their own settings.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<UserResourceResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CreateUserResourceRequest request,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // From the token, never the body — this assignment is what keeps it personal storage.
        request.UserId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
