using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Company.Get;

public class ListCompanyPhotos : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/photos", HandleAsync)
            .AllowAnyAuthenticatedUser(CompanyEndpoints.MemberReadJustification)
            .WithOptionalCompanyScope()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<IReadOnlyList<CompanyPhotoResponse>>, NoContent, ProblemHttpResult>> HandleAsync(
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListCompanyPhotosRequest { CompanyId = companyScope.CompanyId }, cancellationToken);

        return result.ToHttpResult();
    }
}
