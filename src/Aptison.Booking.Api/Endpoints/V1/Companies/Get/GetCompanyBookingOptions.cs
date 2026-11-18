using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Companies.Get;

/// <summary>
/// Everything a customer needs to choose services and a barber. Anonymous, because the booking page
/// is reachable before anyone signs in; only the appointment itself needs an account.
/// </summary>
public class GetCompanyBookingOptions : IVersionedEndpoint
{
    public string Tag => "Companies";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{slug}/booking-options", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CompanyBookingOptionsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        string slug,
        [FromQuery] string? languageCode,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCompanyBookingOptionsRequest { Slug = slug, LanguageCode = languageCode }, cancellationToken);

        return result.ToHttpResult();
    }
}
