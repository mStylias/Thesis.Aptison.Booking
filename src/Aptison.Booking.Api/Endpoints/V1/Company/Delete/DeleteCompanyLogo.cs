using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Company.Delete;

public class DeleteCompanyLogo : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/logo", HandleAsync)
            .RequirePermission(PermissionCodes.Company.Update)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DeleteCompanyLogoRequest { CompanyId = companyScope.RequireCompanyId() }, cancellationToken);

        return result.ToHttpResult();
    }
}
