using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Companies.Get;

/// <summary>
/// A barbershop's public home page. Unconstrained on purpose: a route constraint would turn a
/// malformed slug into a bodyless 404 instead of problem details. The literal <c>/all</c> and
/// <c>/slugs</c> win over it, which is why the slug generator never produces either.
/// </summary>
public class GetCompanyDetails : IVersionedEndpoint
{
    public string Tag => "Companies";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{slug}", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CompanyDetailsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        string slug,
        [FromQuery] string? languageCode,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCompanyDetailsRequest { Slug = slug, LanguageCode = languageCode }, cancellationToken);

        return result.ToHttpResult();
    }
}
