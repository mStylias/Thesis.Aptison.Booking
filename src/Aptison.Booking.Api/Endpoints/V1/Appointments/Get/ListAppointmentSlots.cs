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

/// <summary>The free times on one day for a staff booking. Behind the read code, like the check.</summary>
public class ListAppointmentSlots : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/slots", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<DayAvailabilityResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? localDate,
        [FromQuery] string? serviceIds,
        [FromQuery] Guid? staffMemberId,
        [FromQuery] Guid? customerId,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListAppointmentSlotsRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                LocalDate = localDate,
                ServiceIds = serviceIds,
                StaffMemberId = staffMemberId,
                CustomerId = customerId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
