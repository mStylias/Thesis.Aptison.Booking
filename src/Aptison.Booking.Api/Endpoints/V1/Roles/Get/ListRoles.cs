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
/// The platform's roles and the company's own in one list, with <c>isEditable</c> saying which half
/// this caller may write. The two halves arrive together because the ownership query filter reads
/// the request's company — no union is written anywhere.
/// </summary>
public class ListRoles : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Read)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<RoleResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        ICompanyScope companyScope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListRolesRequest { CompanyId = companyScope.RequireCompanyId() }, cancellationToken);

        return result.ToHttpResult();
    }
}
