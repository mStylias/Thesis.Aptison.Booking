using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>
/// The caller's company and its staff, their permissions there and whether they are a platform
/// administrator.
/// Called after <c>/users/me</c> to decide whether a person sees a staff product or a customer one.
/// </summary>
public class GetCurrentUserWorkspace : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/workspace", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reading your own company and authorization state. A customer's correct answer is " +
                "no company rather than a refusal, and gating it would hide from a client that it " +
                "holds nothing.")
            .WithOptionalCompanyScope()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CurrentUserWorkspaceResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        ICurrentAppUser currentAppUser,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCurrentUserWorkspaceRequest
            {
                UserId = currentAppUser.GetRequiredUserId(),
                CompanyId = companyScope.CompanyId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
