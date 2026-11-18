using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Post;

/// <summary>Confirms, completes or marks a no-show, or restores one of the last two to confirmed.</summary>
public class ChangeAppointmentStatus : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/{id:guid}/status", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentDetailsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        ChangeAppointmentStatusRequest request,
        [FromQuery] string? languageCode,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        request.LanguageCode = languageCode;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
