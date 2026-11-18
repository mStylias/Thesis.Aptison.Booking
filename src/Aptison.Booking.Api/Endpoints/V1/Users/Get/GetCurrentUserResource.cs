using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Application.Features.UserResources.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>
/// One of the caller's own resources. A resource belonging to somebody else answers 404, not 403 —
/// the handler filters by caller, so it is indistinguishable from one that never existed.
/// </summary>
public class GetCurrentUserResource : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/resources/{resourceId:guid}", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reading your own resource. They are personal storage, so no role decides who may " +
                "see them — the handler filters to the caller.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<UserResourceResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid resourceId,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetUserResourceRequest
            {
                UserId = currentAppUser.GetRequiredUserId(),
                ResourceId = resourceId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
