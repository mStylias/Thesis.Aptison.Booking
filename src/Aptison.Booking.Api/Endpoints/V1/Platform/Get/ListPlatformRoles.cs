using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Application.Features.Roles.Queries;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Platform.Get;

/// <summary>
/// The platform's own roles. <c>roles:read</c> counts only if granted globally, so a company
/// administrator is refused here and reads <c>GET /roles</c> instead.
/// </summary>
public class ListPlatformRoles : IVersionedEndpoint
{
    public string Tag => "Platform";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/roles", HandleAsync)
            .RequirePlatformPermission(PermissionCodes.Roles.Read)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<RoleResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ListRolesRequest { CompanyId = null }, cancellationToken);
        return result.ToHttpResult();
    }
}
