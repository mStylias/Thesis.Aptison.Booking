using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.RateLimiting;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Errors;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Post;

/// <summary>
/// Moves one of the caller's own appointments to another time, optionally with other services, a
/// barber or a note. A <c>409 APPOINTMENT_PRICE_CHANGED</c> carries the new <c>totalPrice</c> and
/// changes nothing: the customer confirms it and sends it back.
/// </summary>
public class RescheduleCurrentUserAppointment : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/me/appointments/{id:guid}/reschedule", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Moving your own appointment. The handler scopes the write to the caller and answers " +
                "404 for anyone else's, so there is no permission to hold.")
            .RequireRateLimiting(AppointmentRateLimiter.PolicyName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        RescheduleCurrentUserAppointmentRequest request,
        [FromQuery] string? languageCode,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        request.LanguageCode = languageCode;
        // Overwritten, not merged: this assignment is what stops it being a move-anyone endpoint.
        request.CustomerId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);

        if (result.IsError)
        {
            return result.Error!.ToProblemResult();
        }

        if (result.Data!.Appointment is { } appointment)
        {
            return TypedResults.Ok(appointment);
        }

        return TypedResults.Problem(new ProblemDetails
        {
            Title = ErrorCodes.Appointments.PriceChanged,
            Detail = "The price of this change is different from the one you confirmed. Please confirm the new total.",
            Status = StatusCodes.Status409Conflict,
            Extensions =
            {
                ["totalPrice"] = result.Data.ChangedTotalPrice,
                ["currency"] = result.Data.Currency
            }
        });
    }
}
