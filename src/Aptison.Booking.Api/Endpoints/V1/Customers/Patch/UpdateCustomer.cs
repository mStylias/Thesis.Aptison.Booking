using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.Errors;
using Aptison.Booking.Application.Features.Customers.Commands;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Customers.Patch;

/// <summary>
/// Merge-patch: only the members in the body are saved, and an explicit <c>null</c> clears one.
/// <c>CUSTOMER_EMAIL_USED_BY_WALK_IN</c> carries the other walk-in's <c>customerId</c>.
/// </summary>
public class UpdateCustomer : IVersionedEndpoint
{
    public string Tag => "Customers";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("/{customerId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Customers.Read)
            .RequirePermission(PermissionCodes.Customers.Update)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CustomerResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.CallerUserId = currentAppUser.GetRequiredUserId();
        request.CustomerId = customerId;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResultWithExtensions();
    }
}
