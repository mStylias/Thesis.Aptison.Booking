using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Companies.Queries.OData;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.OData.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Platform.Get;

/// <summary>The superadmin's shop picker. The chosen id goes back in the <c>X-Company-Id</c> header.</summary>
public class ListPlatformCompaniesOData : IVersionedEndpoint
{
    public string Tag => "Platform";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/companies/odata", HandleAsync)
            .RequirePlatformPermission(PermissionCodes.Platform.Manage)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ODataPage>, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ISender sender,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListPlatformCompaniesODataRequest { LanguageCode = languageCode }, cancellationToken);

        return await result.ToODataHttpResultAsync(request, cancellationToken: cancellationToken);
    }
}
