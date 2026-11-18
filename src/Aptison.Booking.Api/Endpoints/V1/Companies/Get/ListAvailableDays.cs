using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Companies.Get;

/// <summary>
/// Which days can take a selection. <c>from</c> and <c>to</c> are the barbershop's own days, and
/// the response echoes the window the server actually answered for.
/// </summary>
public class ListAvailableDays : IVersionedEndpoint
{
    public string Tag => "Companies";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{slug}/availability/days", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AvailableDaysResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        string slug,
        [FromQuery] string? serviceIds,
        [FromQuery] Guid? staffMemberId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListAvailableDaysRequest
            {
                Slug = slug,
                ServiceIds = serviceIds,
                StaffMemberId = staffMemberId,
                From = from,
                To = to
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
