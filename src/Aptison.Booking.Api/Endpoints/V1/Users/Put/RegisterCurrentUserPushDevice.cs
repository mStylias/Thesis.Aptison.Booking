using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Put;

/// <summary>
/// Registers the caller's current browser for reminder pushes. A PUT because registering the same
/// browser again is the expected call on every app start, not an error.
/// </summary>
public class RegisterCurrentUserPushDevice : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/me/push-devices", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Turning on reminder pushes for your own browser. Every customer gets reminders, so there " +
                "is no permission to hold.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<Results<Ok<PushDeviceResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        RegisterPushDeviceRequest request,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Overwritten, not merged: this assignment is what keeps the device the caller's own.
        request.UserId = currentAppUser.GetRequiredUserId();

        return (await sender.SendAsync(request, cancellationToken)).ToHttpResult();
    }
}
