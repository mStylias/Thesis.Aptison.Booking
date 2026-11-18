using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Application.Features.Roles.Queries;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Roles.Get;

/// <summary>
/// The permission catalog answered for this caller at this company: what a role editor may offer,
/// and what it should grey out. Under <c>/roles</c> rather than <c>/permissions</c> because the
/// question is "which codes may I put in a role here", not "which codes exist".
/// </summary>
public class ListGrantablePermissions : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/grantable-permissions", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Read)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<GrantablePermissionResponse>>, NoContent, ProblemHttpResult>>
        HandleAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListGrantablePermissionsRequest(), cancellationToken);

        return result.ToHttpResult();
    }
}
