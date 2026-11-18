using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Platform.Get;

public class GetUser : IVersionedEndpoint
{
    public string Tag => "Platform";
    public ApiVersion Version => new(1);

    // ProblemHttpResult has no statically-known status code, so the problem responses below must be
    // declared explicitly.
    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/users/{id:guid}", HandleAsync)
            .RequirePlatformPermission(PermissionCodes.Users.Read)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<UserResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetUserRequest { Id = id }, cancellationToken);
        return result.ToHttpResult();
    }
}
