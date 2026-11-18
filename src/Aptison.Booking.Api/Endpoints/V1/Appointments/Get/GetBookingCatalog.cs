using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Get;

/// <summary>The services and each barber's offerings a staff create or edit can choose from.</summary>
public class GetBookingCatalog : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/booking-catalog", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentCatalogResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetBookingCatalogRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                LanguageCode = languageCode
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
