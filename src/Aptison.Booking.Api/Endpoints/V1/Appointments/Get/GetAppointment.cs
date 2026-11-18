using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Get;

/// <summary>
/// One of the company's appointments, as the management drawer opens it. The customer's own reads
/// are at <c>GET /users/me/appointments/{id}</c>.
/// </summary>
public class GetAppointment : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    // The `:guid` constraint keeps this from swallowing /odata and /calendar-options.
    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{id:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentDetailsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        [FromQuery] string? languageCode,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetAppointmentRequest { Id = id, LanguageCode = languageCode }, cancellationToken);

        return result.ToHttpResult();
    }
}
