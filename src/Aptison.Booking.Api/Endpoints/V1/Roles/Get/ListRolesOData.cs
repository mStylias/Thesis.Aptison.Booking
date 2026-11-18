using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Queries.OData;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.OData.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Roles.Get;

/// <summary>
/// The same roles <see cref="ListRoles"/> shows, as a queryable grid carrying a user count
/// instead of the permission codes.
/// </summary>
public class ListRolesOData : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/odata", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Read)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ODataPage>, ProblemHttpResult>> HandleAsync(
        ISender sender, HttpRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ListRolesODataRequest(), cancellationToken);
        return await result.ToODataHttpResultAsync(request, cancellationToken: cancellationToken);
    }
}
