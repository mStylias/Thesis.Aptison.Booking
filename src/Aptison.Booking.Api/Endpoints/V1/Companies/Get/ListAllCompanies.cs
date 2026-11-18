using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Companies.Get;

/// <summary>
/// Every barbershop from the configured origin, cached whole — the home screen's warm list. The
/// same read <see cref="ListCompanies"/> does with no filters, kept on its own route so the cache
/// key never has to encode a filter set.
/// <para>
/// The literal segment does not collide with a future <c>/companies/{companyId:guid}</c>: the
/// <c>:guid</c> constraint does not match <c>all</c>.
/// </para>
/// </summary>
public class ListAllCompanies : IVersionedEndpoint
{
    public string Tag => "Companies";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/all", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<CompanyCardResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListAllCompaniesRequest { LanguageCode = languageCode }, cancellationToken);

        return result.ToHttpResult();
    }
}
