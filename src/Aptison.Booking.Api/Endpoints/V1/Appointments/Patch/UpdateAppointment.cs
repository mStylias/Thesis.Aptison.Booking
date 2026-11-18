using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Patch;

/// <summary>
/// Changes one of the company's appointments. <c>application/merge-patch+json</c>: an absent member
/// is unchanged. The ownership filter makes another shop's appointment a 404.
/// </summary>
public class UpdateAppointment : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("/{id:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.Update)
            .Accepts<UpdateAppointmentRequest>("application/merge-patch+json", "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentDetailsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        UpdateAppointmentRequest request,
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
