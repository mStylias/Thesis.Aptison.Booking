using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Errors;
using Synax.AspNetCore.Results;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics;

/// <summary>One dashboard tab: every chart on it in one response. A derived endpoint names only its route.</summary>
public abstract class AnalyticsTabEndpoint<TRequest, TResponse> : IVersionedEndpoint
    where TRequest : AnalyticsRequest, IQuery<Result<TResponse, HttpError>>, new()
{
    public string Tag => "Analytics";
    public ApiVersion Version => new(1);

    protected abstract string Route { get; }

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequirePermission(PermissionCodes.Analytics.Read)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<TResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [AsParameters] AnalyticsQueryParameters parameters,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            parameters.ToRequest<TRequest>(companyScope.RequireCompanyId()), cancellationToken);

        return result.ToHttpResult();
    }
}
