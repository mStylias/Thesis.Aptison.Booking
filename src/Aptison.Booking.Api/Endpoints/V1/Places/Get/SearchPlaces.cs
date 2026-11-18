using Aptison.Booking.Api.RateLimiting;
using Aptison.Booking.Application.Features.Places.Common.Responses;
using Aptison.Booking.Application.Features.Places.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Places.Get;

/// <summary>
/// Place suggestions for a partial query, best match first. Nothing matched is <c>200</c> with an
/// empty array — a searched-and-found-nothing answer the client must be able to tell from a fault.
/// </summary>
public class SearchPlaces : IVersionedEndpoint
{
    public string Tag => "Places";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/", HandleAsync)
            .AllowAnonymous()
            // Every call past the cache is billed, so the limit is the budget's floor, not polish.
            .RequireRateLimiting(PlaceSearchRateLimiter.PolicyName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<Results<Ok<IReadOnlyList<PlaceSuggestionResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        [AsParameters] SearchPlacesRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(request, cancellationToken);

        return result.ToHttpResult();
    }
}
