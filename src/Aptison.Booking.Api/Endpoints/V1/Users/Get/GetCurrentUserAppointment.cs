using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>
/// Reads one of the caller's own appointments back — the success screen is its own route, so a
/// refresh lands here.
/// </summary>
public class GetCurrentUserAppointment : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/appointments/{id:guid}", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reading your own appointment. The handler scopes the read to the caller and " +
                "answers 404 for anyone else's, so there is no permission to hold.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        [FromQuery] string? languageCode,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var request = new GetCurrentUserAppointmentRequest
        {
            Id = id,
            LanguageCode = languageCode,
            // From the token: this assignment is what keeps the route to the caller's own bookings.
            CustomerId = currentAppUser.GetRequiredUserId()
        };

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
