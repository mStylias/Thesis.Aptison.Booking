using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Application.Features.Services.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Services.Get;

public class GetServiceOptions : IVersionedEndpoint
{
    public string Tag => "Services";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/options", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Read)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ServiceOptionsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetServiceOptionsRequest { CompanyId = companyScope.RequireCompanyId(), LanguageCode = languageCode },
            cancellationToken);

        return result.ToHttpResult();
    }
}
