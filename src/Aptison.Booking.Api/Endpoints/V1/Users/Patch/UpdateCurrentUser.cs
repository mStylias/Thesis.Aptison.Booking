using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Users.Commands;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Patch;

/// <summary>
/// Completes the caller's own profile. Partial update: only properties present in the body are
/// applied, and an explicit <c>null</c> clears the phone number.
/// </summary>
public class UpdateCurrentUser : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("/me", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Completing your own profile. Gating it would strand anyone whose role lacked the " +
                "code with a permanently incomplete account.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<UserResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        UpdateCurrentUserRequest request,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Overwritten, not merged: this assignment is what stops it being an edit-anyone endpoint.
        request.Id = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
