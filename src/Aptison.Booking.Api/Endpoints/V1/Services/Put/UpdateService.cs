using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Services.Commands;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Services.Put;

/// <summary>A full replace, the barbers included.</summary>
public class UpdateService : IVersionedEndpoint
{
    public string Tag => "Services";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/{serviceId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ServiceResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid serviceId,
        UpdateServiceRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.ServiceId = serviceId;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
