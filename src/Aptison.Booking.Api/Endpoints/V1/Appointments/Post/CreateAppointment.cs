using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Post;

/// <summary>
/// Staff book an appointment for a shop customer or a new walk-in. The customer's own booking is
/// at <c>POST /users/me/appointments</c>.
/// </summary>
public class CreateAppointment : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.Create)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentDetailsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CreateAppointmentRequest request,
        [FromQuery] string? languageCode,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.LanguageCode = languageCode;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
