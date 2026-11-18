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
/// Who holds which roles at this company. Company-scoped grants only — a user's global roles are
/// platform facts and are not this company's to read.
/// </summary>
public class ListRoleAssignments : IVersionedEndpoint
{
    public string Tag => "Roles";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/assignments", HandleAsync)
            .RequirePermission(PermissionCodes.Roles.Read)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<RoleAssignmentResponse>>, NoContent, ProblemHttpResult>>
        HandleAsync(Guid? userId, ICompanyScope companyScope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListRoleAssignmentsRequest { CompanyId = companyScope.RequireCompanyId(), UserId = userId },
            cancellationToken);

        return result.ToHttpResult();
    }
}
