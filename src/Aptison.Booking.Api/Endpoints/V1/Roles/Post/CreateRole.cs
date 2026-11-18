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
/// Creates a role the company owns. <c>roles:create</c> gates the call; what the role may contain
/// is decided in the handler, which is the only layer that sees the body.
/// </summary>
public class CreateRole : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Create)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<RoleResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CreateRoleRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // From the signed-in user, never from the body — the property is [JsonIgnore]'d.
        request.CompanyId = companyScope.RequireCompanyId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
