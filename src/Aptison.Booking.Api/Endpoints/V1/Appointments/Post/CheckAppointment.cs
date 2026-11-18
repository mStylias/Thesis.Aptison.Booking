using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Post;

/// <summary>
/// The warnings for a proposed staff booking or edit — a read with a body, which changes nothing.
/// Behind the read code, since it tells nothing a calendar does not already show.
/// </summary>
public class CheckAppointment : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/check", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AppointmentCheckResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CheckAppointmentRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
