using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Company.Put;

public class ReorderCompanyPhotos : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/photos/order", HandleAsync)
            .RequirePermission(PermissionCodes.Company.Update)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<CompanyPhotoResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        ReorderCompanyPhotosRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
