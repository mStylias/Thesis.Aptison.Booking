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

/// <summary>How full each day in a range is for a staff booking. Behind the read code, like the slots.</summary>
public class ListAppointmentDays : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/days", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AvailableDaysResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? serviceIds,
        [FromQuery] Guid? staffMemberId,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? appointmentId,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListAppointmentDaysRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                From = from,
                To = to,
                ServiceIds = serviceIds,
                StaffMemberId = staffMemberId,
                CustomerId = customerId,
                AppointmentId = appointmentId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
