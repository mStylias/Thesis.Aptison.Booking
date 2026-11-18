using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.OData.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Get;

/// <summary>
/// The shop's services for the appointments grid's filter. Under <c>appointments</c> rather than
/// <c>services</c> because <c>staff</c> holds <c>appointments:read_all</c> but not <c>services:read</c>.
/// </summary>
public class ListServiceOptionsOData : IVersionedEndpoint
{
    private static readonly SynaxODataOptions ODataOptions =
        new SynaxODataOptions().Searchable<ServiceOptionODataResponse>(service => service.NameNormalized);

    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/services/odata", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ODataPage>, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ISender sender,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListServiceOptionsODataRequest { LanguageCode = languageCode }, cancellationToken);

        return await result.ToODataHttpResultAsync(request, ODataOptions, cancellationToken);
    }
}
