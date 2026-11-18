using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Commands;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Roles.Delete;

/// <summary>
/// Soft-deletes a company-owned role and revokes it from everyone holding it. The one write whose
/// cache invalidation reaches more than a single entry.
/// </summary>
public class DeleteRole : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/{roleId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Delete)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid roleId, ICompanyScope companyScope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DeleteRoleRequest { CompanyId = companyScope.RequireCompanyId(), RoleId = roleId },
            cancellationToken);

        return result.ToHttpResult();
    }
}
