using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Company.Post;

public class CreateCompanyHoliday : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/holidays", HandleAsync)
            .RequirePermission(PermissionCodes.Company.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Created<CompanyScheduleChangeResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CreateCompanyHolidayRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult(change => TypedResults.Created(
            $"/api/v1/company/holidays/{change.Holiday!.Id}", change));
    }
}
