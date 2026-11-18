using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Reviews.Common.Responses;
using Aptison.Booking.Application.Features.Reviews.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>The caller's reviews at every barbershop, newest first.</summary>
public class ListCurrentUserReviews : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/reviews", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Listing your own reviews. The handler scopes the read to the caller, so there is no " +
                "permission to hold.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ReviewPageResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? languageCode,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListCurrentUserReviewsRequest
            {
                Page = page,
                PageSize = pageSize,
                LanguageCode = languageCode,
                // From the token: this assignment is what keeps the list to the caller's own reviews.
                CustomerId = currentAppUser.GetRequiredUserId()
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
