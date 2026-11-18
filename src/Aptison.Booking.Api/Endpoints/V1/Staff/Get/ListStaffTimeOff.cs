using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Queries;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Staff.Get;

/// <summary>Any member's time off with <c>members:read</c>. Without it, only the caller's own.</summary>
public class ListStaffTimeOff : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{staffMemberId:guid}/time-off", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Every staff member may read their own time off. The handler requires members:read for " +
                "anyone else's, and a company for both.")
            .WithOptionalCompanyScope()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<StaffTimeOffResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid staffMemberId,
        [FromQuery] bool? past,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListStaffTimeOffRequest
            {
                CompanyId = companyScope.CompanyId,
                CallerUserId = currentAppUser.GetRequiredUserId(),
                StaffMemberId = staffMemberId,
                Past = past ?? false
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
