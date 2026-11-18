using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.ServiceCategories.Commands;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Services.Post;

/// <summary>
/// Under <c>services</c> because Synax takes the route prefix from the tag. <c>services:update</c>:
/// a category is part of the catalog, and no category code exists.
/// </summary>
public class CreateServiceCategory : IVersionedEndpoint
{
    public string Tag => "Services";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/categories", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Created<ServiceCategoryResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CreateServiceCategoryRequest request,
        [FromQuery] string? languageCode,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.LanguageCode = languageCode;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult(category =>
            TypedResults.Created($"/api/v1/services/categories/{category.Id}", category));
    }
}
