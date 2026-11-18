using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.UserResources.Queries.OData;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.OData.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>
/// The same resources <see cref="ListCurrentUserResources"/> shows, as a client-queryable grid. No
/// company in the route and none wanted: these rows are scoped to the caller, and the handler
/// applies that filter before any <c>$filter</c> reaches the query.
/// </summary>
public class ListCurrentUserResourcesOData : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/resources/odata", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reading your own resources. They are personal storage, so no role decides who may " +
                "see them — the handler filters to the caller.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ODataPage>, ProblemHttpResult>> HandleAsync(
        ICurrentAppUser currentAppUser,
        ISender sender,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListUserResourcesODataRequest { UserId = currentAppUser.GetRequiredUserId() },
            cancellationToken);

        return await result.ToODataHttpResultAsync(request, cancellationToken: cancellationToken);
    }
}
