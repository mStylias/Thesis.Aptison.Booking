using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Company.Get;

public class GetCompanySettings : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/", HandleAsync)
            .AllowAnyAuthenticatedUser(CompanyEndpoints.MemberReadJustification)
            .WithOptionalCompanyScope()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CompanySettingsResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? languageCode,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCompanySettingsRequest { CompanyId = companyScope.CompanyId, LanguageCode = languageCode },
            cancellationToken);

        return result.ToHttpResult();
    }
}
