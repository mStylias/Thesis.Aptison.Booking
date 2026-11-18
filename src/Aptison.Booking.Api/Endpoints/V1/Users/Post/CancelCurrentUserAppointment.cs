using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Post;

/// <summary>Cancels one of the caller's own appointments, until the barbershop's cancellation cutoff.</summary>
public class CancelCurrentUserAppointment : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/me/appointments/{id:guid}/cancel", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Cancelling your own appointment. The handler scopes the write to the caller and " +
                "answers 404 for anyone else's, so there is no permission to hold.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        CancelCurrentUserAppointmentRequest? request,
        [FromQuery] string? languageCode,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // The reason is optional, so is the body.
        request ??= new CancelCurrentUserAppointmentRequest();
        request.Id = id;
        request.LanguageCode = languageCode;
        // Overwritten, not merged: this assignment is what stops it being a cancel-anyone endpoint.
        request.CustomerId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
