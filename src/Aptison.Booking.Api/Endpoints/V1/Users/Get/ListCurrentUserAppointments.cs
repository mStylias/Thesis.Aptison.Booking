using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Enums;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Users.Get;

/// <summary>The caller's appointments at every barbershop, one page of upcoming or past ones.</summary>
public class ListCurrentUserAppointments : IVersionedEndpoint
{
    public string Tag => "Users";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/me/appointments", HandleAsync)
            .AllowAnyAuthenticatedUser(
                "Listing your own appointments. The handler scopes the read to the caller, so there " +
                "is no permission to hold.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentPageResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] AppointmentPeriod period,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? languageCode,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var request = new ListCurrentUserAppointmentsRequest
        {
            Period = period,
            Page = page,
            PageSize = pageSize,
            LanguageCode = languageCode,
            // From the token: this assignment is what keeps the list to the caller's own bookings.
            CustomerId = currentAppUser.GetRequiredUserId()
        };

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
