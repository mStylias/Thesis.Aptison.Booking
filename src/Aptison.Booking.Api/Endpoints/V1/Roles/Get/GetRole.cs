using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Application.Features.Roles.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Roles.Get;

/// <summary>
/// One role as this company sees it. Another company's role is a 404, because the ownership filter
/// removed the row before the handler ran.
/// </summary>
public class GetRole : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    // The `:guid` constraint keeps this from swallowing the sibling routes under the same prefix.
    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{roleId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Read)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<RoleResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid roleId, ICompanyScope companyScope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetRoleRequest { CompanyId = companyScope.RequireCompanyId(), RoleId = roleId },
            cancellationToken);

        return result.ToHttpResult();
    }
}
