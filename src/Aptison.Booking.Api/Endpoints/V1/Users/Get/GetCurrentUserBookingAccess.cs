using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

public class GetCurrentUserBookingAccess : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/booking-access/{slug}", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Asking whether you yourself may book a shop online. The answer is about the caller " +
                "only and names no reason, so there is no permission to hold.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<BookingAccessResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        string slug, ICurrentAppUser currentAppUser, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCurrentUserBookingAccessRequest { CustomerId = currentAppUser.GetRequiredUserId(), Slug = slug },
            cancellationToken);

        return result.ToHttpResult();
    }
}
