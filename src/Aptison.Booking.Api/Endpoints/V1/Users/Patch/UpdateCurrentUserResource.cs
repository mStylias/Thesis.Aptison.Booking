using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.UserResources.Commands;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Patch;

/// <summary>
/// Updates one of the caller's own resources. Partial update: only properties present in the body
/// are applied, and <c>value</c> is replaced whole rather than merged.
/// </summary>
public class UpdateCurrentUserResource : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("/me/resources/{resourceId:guid}", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Updating your own resource. Gating it would leave a signed-in user unable to change " +
                "their own settings.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<UserResourceResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid resourceId,
        UpdateUserResourceRequest request,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.UserId = currentAppUser.GetRequiredUserId();
        request.ResourceId = resourceId;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
