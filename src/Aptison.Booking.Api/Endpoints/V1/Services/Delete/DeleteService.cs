using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Services.Commands;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Services.Delete;

/// <summary>Refused with a 409 once the service is on any appointment.</summary>
public class DeleteService : IVersionedEndpoint
{
    public string Tag => "Services";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/{serviceId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Delete)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid serviceId, ICompanyScope companyScope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DeleteServiceRequest { CompanyId = companyScope.RequireCompanyId(), ServiceId = serviceId },
            cancellationToken);

        return result.ToHttpResult();
    }
}
