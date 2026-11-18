using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>
/// The caller's own staff record, or <c>404 USER_NOT_STAFF_MEMBER</c>. No company scope, so
/// <c>X-Company-Id</c> cannot change the answer.
/// </summary>
public class GetCurrentUserStaffMember : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/staff-member", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reading your own staff record and weekly hours. Every staff member may see their own " +
                "week, and the answer never describes anyone else.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CurrentStaffMemberResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCurrentUserStaffMemberRequest { UserId = currentAppUser.GetRequiredUserId() },
            cancellationToken);

        return result.ToHttpResult();
    }
}
