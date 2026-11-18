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

public class UnblockCustomer : IVersionedEndpoint
{
    public string Tag => "Customers";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/{customerId:guid}/unblock", HandleAsync)
            .RequirePermission(PermissionCodes.Customers.Read)
            .RequirePermission(PermissionCodes.Customers.Block)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CustomerResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid customerId,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new UnblockCustomerRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                CallerUserId = currentAppUser.GetRequiredUserId(),
                CustomerId = customerId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
