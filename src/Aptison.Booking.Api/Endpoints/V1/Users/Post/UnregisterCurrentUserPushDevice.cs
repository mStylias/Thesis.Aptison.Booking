using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Post;

/// <summary>
/// Removes the caller's current browser by its endpoint: push off on this device, and before
/// sign-out. A POST rather than a DELETE with a body, which some proxies drop.
/// </summary>
public class UnregisterCurrentUserPushDevice : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/me/push-devices/unregister", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Turning off reminder pushes for your own browser. Gating it could leave pushes arriving " +
                "on a shared computer after sign-out.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        UnregisterPushDeviceRequest request,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Overwritten, not merged: this assignment is what stops it removing someone else's device.
        request.UserId = currentAppUser.GetRequiredUserId();

        return (await sender.SendAsync(request, cancellationToken)).ToHttpResult();
    }
}
