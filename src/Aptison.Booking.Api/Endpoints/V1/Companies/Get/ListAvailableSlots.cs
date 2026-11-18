using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Companies.Get;

/// <summary>The bookable times on one of the barbershop's own days.</summary>
public class ListAvailableSlots : IVersionedEndpoint
{
    public string Tag => "Companies";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{slug}/availability/slots", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<DayAvailabilityResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        string slug,
        [FromQuery] string? serviceIds,
        [FromQuery] Guid? staffMemberId,
        [FromQuery] DateOnly? date,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListAvailableSlotsRequest
            {
                Slug = slug,
                ServiceIds = serviceIds,
                StaffMemberId = staffMemberId,
                Date = date
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
