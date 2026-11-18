using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Customers.Commands;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Customers.Post;

/// <summary>Cannot be undone. Answers the kept customer; the removed id answers not found from then on.</summary>
public class MergeCustomers : IVersionedEndpoint
{
    public string Tag => "Customers";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/merge", HandleAsync)
            .RequirePermission(PermissionCodes.Customers.Read)
            .RequirePermission(PermissionCodes.Customers.Merge)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CustomerResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        MergeCustomersRequest request,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.CallerUserId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
