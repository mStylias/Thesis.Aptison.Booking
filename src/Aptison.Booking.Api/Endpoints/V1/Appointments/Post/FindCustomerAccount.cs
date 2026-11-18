using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.RateLimiting;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Post;

/// <summary>The address travels in the body so it stays out of access logs.</summary>
public class FindCustomerAccount : IVersionedEndpoint
{
    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/customers/account-lookup", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.Create)
            .RequireRateLimiting(CustomerAccountLookupRateLimiter.PolicyName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<Results<Ok<CustomerAccountLookupResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        FindCustomerAccountRequest request, ISender sender, CancellationToken cancellationToken)
        => (await sender.SendAsync(request, cancellationToken)).ToHttpResult();
}
