using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Reviews.Commands;
using Aptison.Booking.Application.Features.Reviews.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Post;

/// <summary>Reviews one of the caller's completed visits, up to 30 days after it.</summary>
public class CreateCurrentUserReview : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/me/appointments/{id:guid}/review", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Reviewing your own visit. The handler scopes the write to the caller's appointments, " +
                "so there is no permission to hold.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ReviewResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        CreateCurrentUserReviewRequest request,
        [FromQuery] string? languageCode,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.AppointmentId = id;
        request.LanguageCode = languageCode;
        // Overwritten, not merged: this assignment is what stops it reviewing anyone's visit.
        request.CustomerId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
