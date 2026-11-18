using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.UserResources.Commands;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Delete;

/// <summary>Soft-deletes one of the caller's own resources.</summary>
public class DeleteCurrentUserResource : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/me/resources/{resourceId:guid}", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Deleting your own resource. Gating it would leave a signed-in user unable to remove " +
                "what they saved.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid resourceId,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DeleteUserResourceRequest
            {
                UserId = currentAppUser.GetRequiredUserId(),
                ResourceId = resourceId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
