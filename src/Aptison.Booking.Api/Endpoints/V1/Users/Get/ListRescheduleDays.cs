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

/// <summary>Which days one of the caller's appointments can move to. Its current time counts as free.</summary>
public class ListRescheduleDays : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/appointments/{id:guid}/availability/days", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Finding a new time for your own appointment. The handler scopes the read to the " +
                "caller, so there is no permission to hold.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AvailableDaysResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        [FromQuery] string? serviceIds,
        [FromQuery] Guid? staffMemberId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListRescheduleDaysRequest
            {
                AppointmentId = id,
                ServiceIds = serviceIds,
                StaffMemberId = staffMemberId,
                From = from,
                To = to,
                // From the token: this assignment is what keeps the route to the caller's own bookings.
                CustomerId = currentAppUser.GetRequiredUserId()
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
