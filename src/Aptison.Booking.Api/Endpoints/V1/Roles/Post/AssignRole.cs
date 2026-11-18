using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Commands;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Roles.Post;

/// <summary>
/// Grants a role to a member of this company's staff. <c>roles:assign</c> gates the call; the
/// handler decides two further invariants, chiefly that a caller may only grant permissions they
/// hold here — without it, <c>roles:assign</c> alone could grant <c>platform:manage</c>.
/// </summary>
public class AssignRole : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/assignments", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Assign)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<RoleAssignmentResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        AssignRoleRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
