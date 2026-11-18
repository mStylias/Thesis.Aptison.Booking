using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Services.Common.ResponseOData;
using Aptison.Booking.Application.Features.Services.Queries.OData;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.OData.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Services.Get;

/// <summary>The management services grid. <c>$top</c> is capped at 100.</summary>
public class ListServicesOData : IVersionedEndpoint
{
    private static readonly SynaxODataOptions ODataOptions =
        new SynaxODataOptions().Searchable<ServiceODataResponse>(service => service.NameNormalized);

    public string Tag => "Services";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/odata", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Read)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ODataPage>, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ISender sender,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListServicesODataRequest { LanguageCode = languageCode }, cancellationToken);

        return await result.ToODataHttpResultAsync(request, ODataOptions, cancellationToken);
    }
}
