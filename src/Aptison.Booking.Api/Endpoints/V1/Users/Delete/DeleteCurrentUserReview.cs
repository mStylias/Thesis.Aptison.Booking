using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Reviews.Commands;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Delete;

/// <summary>Deletes one of the caller's own reviews. It stops counting toward the ratings at once.</summary>
public class DeleteCurrentUserReview : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/me/reviews/{id:guid}", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Deleting your own review. The handler scopes the write to the caller and answers 404 " +
                "for anyone else's, so there is no permission to hold.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DeleteCurrentUserReviewRequest
            {
                Id = id,
                // From the token: this assignment is what stops it deleting anyone's review.
                CustomerId = currentAppUser.GetRequiredUserId()
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
