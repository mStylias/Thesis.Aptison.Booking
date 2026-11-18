using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.RateLimiting;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Post;

/// <summary>
/// Books an appointment for the caller. The barbershop is named in the body rather than the route:
/// a customer is not staff there, so there is no company-scoped permission to resolve — the handler
/// widens its own reads instead. Staff book at <c>POST /appointments</c>.
/// </summary>
public class CreateCurrentUserAppointment : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/me/appointments", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Booking an appointment as a customer. Every customer may do it, so there is no " +
                "permission to hold; the verified-email gate on the fallback policy still applies.")
            .RequireRateLimiting(AppointmentRateLimiter.PolicyName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CreateCurrentUserAppointmentRequest request,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Overwritten, not merged: this assignment is what stops it being a book-for-anyone endpoint.
        request.CustomerId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
