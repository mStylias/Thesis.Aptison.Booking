using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Companies.Get;

/// <summary>Every active shop's slug for the web app's sitemap. 204 when there are none.</summary>
public class ListCompanySitemapEntries : IVersionedEndpoint
{
    public string Tag => "Companies";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/slugs", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<CompanySitemapEntryResponse>?>, NoContent, ProblemHttpResult>> HandleAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ListCompanySitemapEntriesRequest(), cancellationToken);

        return result.ToHttpResult();
    }
}
