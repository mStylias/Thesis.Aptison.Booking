using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Commands;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Roles.Put;

/// <summary>
/// Replaces a company-owned role. A platform-provided role is a 403 here, and a system role is a
/// 403 for everyone — <c>is_system</c> finally means something.
/// </summary>
public class UpdateRole : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/{roleId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<RoleResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.RoleId = roleId;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
