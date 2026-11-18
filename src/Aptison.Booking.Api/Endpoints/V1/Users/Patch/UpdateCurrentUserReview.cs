using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Reviews.Commands;
using Aptison.Booking.Application.Features.Reviews.Common.Responses;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Patch;

/// <summary>Edits the stars or the comment of one of the caller's own reviews.</summary>
public class UpdateCurrentUserReview : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("/me/reviews/{id:guid}", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Editing your own review. The handler scopes the write to the caller and answers 404 " +
                "for anyone else's, so there is no permission to hold.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ReviewResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        UpdateCurrentUserReviewRequest request,
        [FromQuery] string? languageCode,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        request.LanguageCode = languageCode;
        // Overwritten, not merged: this assignment is what stops it editing anyone's review.
        request.CustomerId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
