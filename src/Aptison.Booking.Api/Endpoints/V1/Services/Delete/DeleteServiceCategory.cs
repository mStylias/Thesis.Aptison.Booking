using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.ServiceCategories.Commands;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Services.Delete;

/// <summary>Its services become uncategorised. A platform category is a 403.</summary>
public class DeleteServiceCategory : IVersionedEndpoint
{
    public string Tag => "Services";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/categories/{categoryId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Update)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid categoryId, ICompanyScope companyScope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DeleteServiceCategoryRequest { CompanyId = companyScope.RequireCompanyId(), CategoryId = categoryId },
            cancellationToken);

        return result.ToHttpResult();
    }
}
