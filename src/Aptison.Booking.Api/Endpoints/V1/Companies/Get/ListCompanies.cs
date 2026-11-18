using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Companies.Get;

/// <summary>
/// The public barbershop list, ordered by distance. Unpaged and uncapped: every match comes back.
/// </summary>
public class ListCompanies : IVersionedEndpoint
{
    public string Tag => "Companies";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <remarks>
    /// Bound straight onto the request: every one of its properties is a query parameter, and with
    /// no caller identity involved there is nothing a client could bind that it should not.
    /// </remarks>
    private static async Task<Results<Ok<IReadOnlyList<CompanyCardResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        [AsParameters] ListCompaniesRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(request, cancellationToken);

        return result.ToHttpResult();
    }
}
