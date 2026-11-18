using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Application.Features.Services.Queries;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Services.Get;

public class GetService : IVersionedEndpoint
{
    public string Tag => "Services";
    public ApiVersion Version => new(1);

    // The guid constraint keeps this from swallowing /options and /odata.
    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{serviceId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Read)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ServiceResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid serviceId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetServiceRequest { ServiceId = serviceId }, cancellationToken);
        return result.ToHttpResult();
    }
}
