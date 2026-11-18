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

public class UpdateCompanyPhoto : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/photos/{photoId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Company.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CompanyPhotoResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid photoId,
        UpdateCompanyPhotoRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.PhotoId = photoId;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
