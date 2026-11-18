using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Application.Features.Customers.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Customers.Get;

public class GetCustomer : IVersionedEndpoint
{
    public string Tag => "Customers";
    public ApiVersion Version => new(1);

    // The guid constraint keeps this from swallowing /odata.
    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/{customerId:guid}", HandleAsync)
            .RequirePermission(PermissionCodes.Customers.Read)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CustomerResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid customerId, ICompanyScope companyScope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new GetCustomerRequest { CompanyId = companyScope.RequireCompanyId(), CustomerId = customerId },
            cancellationToken);

        return result.ToHttpResult();
    }
}
