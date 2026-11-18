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

namespace Aptison.Booking.Api.Endpoints.V1.Services.Put;

/// <summary>Renames a shop-owned category. A platform category is a 403.</summary>
public class UpdateServiceCategory : IVersionedEndpoint
{
    public string Tag => "Services";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/categories/{categoryId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Services.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ServiceCategoryResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid categoryId,
        UpdateServiceCategoryRequest request,
        [FromQuery] string? languageCode,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.CategoryId = categoryId;
        request.LanguageCode = languageCode;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
