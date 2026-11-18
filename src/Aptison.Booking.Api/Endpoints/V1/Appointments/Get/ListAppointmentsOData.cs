using Aptison.Booking.Api.Auth.Authorization;
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
/// The company's appointments for the management calendar and the appointments grid, and with
/// <c>$apply</c> the calendar's per-day counts. No page size: without <c>$top</c> every match is
/// returned, so the calendar always narrows by <c>localDate</c> and the grid pages with <c>$top</c>,
/// which is capped at 100.
/// </summary>
public class ListAppointmentsOData : IVersionedEndpoint
{
    /// <summary>Its own instance so <c>$apply</c> is on here and nowhere else.</summary>
    private static readonly SynaxODataOptions ODataOptions = new()
    {
        EnableApply = true,
        AllowedAggregations = { "$count" }
    };

    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/odata", HandleAsync)
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
            new ListAppointmentsODataRequest { LanguageCode = languageCode }, cancellationToken);

        return await result.ToODataHttpResultAsync(request, ODataOptions, cancellationToken);
    }
}
