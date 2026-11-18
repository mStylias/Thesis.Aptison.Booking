using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics.Get;

/// <summary>One chart, by the name of its property on the tab response, e.g. <c>/charts/revenuePerStaff</c>.</summary>
public class GetAnalyticsChart : IVersionedEndpoint
{
    public string Tag => "Analytics";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/charts/{chart}", HandleAsync)
            .RequirePermission(PermissionCodes.Analytics.Read)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<AnalyticsChartResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        AnalyticsChart chart,
        [AsParameters] AnalyticsQueryParameters parameters,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var request = parameters.ToRequest<GetAnalyticsChartRequest>(companyScope.RequireCompanyId());
        request.Chart = chart;

        return (await sender.SendAsync(request, cancellationToken)).ToHttpResult();
    }
}
