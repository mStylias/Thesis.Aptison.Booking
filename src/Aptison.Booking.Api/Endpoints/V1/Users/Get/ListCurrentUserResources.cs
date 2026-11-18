using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Application.Features.UserResources.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>The caller's own stored resources, by name.</summary>
public class ListCurrentUserResources : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/resources", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reading your own resources. They are personal storage, so no role decides who may " +
                "see them — the handler filters to the caller.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<UserResourceResponse>>, NoContent, ProblemHttpResult>>
        HandleAsync(ICurrentAppUser currentAppUser, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListUserResourcesRequest { UserId = currentAppUser.GetRequiredUserId() }, cancellationToken);

        return result.ToHttpResult();
    }
}
