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

/// <summary>
/// The management calendar's time zone, weekly hours, staff list and categories. Behind the same
/// code as the appointments, since <c>staff</c> holds neither <c>members:read</c> nor <c>users:read</c>.
/// </summary>
public class GetCalendarOptions : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/calendar-options", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CalendarOptionsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCalendarOptionsRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                LanguageCode = languageCode
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
