using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.OData;
using Aptison.Booking.Application.Features.Customers.Common.ResponseOData;
using Aptison.Booking.Application.Features.Customers.Queries.OData;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.OData.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Customers.Get;

/// <summary>
/// The customers page, paged with <c>$top</c> (at most 100) and read page by page for the export.
/// <c>$search</c> reads the name, the email and the phone; a term with digits or <c>@</c> must be
/// a quoted phrase.
/// </summary>
public class ListCustomersOData : IVersionedEndpoint
{
    private static readonly IReadOnlySet<string> SpendMembers = new HashSet<string>(StringComparer.Ordinal)
    {
        "totalSpend",
        "averageSpend"
    };

    private static readonly SynaxODataOptions ODataOptions = new SynaxODataOptions()
        .Searchable<CustomerODataResponse>(
            customer => customer.NameNormalized, customer => customer.Email, customer => customer.PhoneNumber);

    public string Tag => "Customers";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/odata", HandleAsync)
            .RequirePermission(PermissionCodes.Customers.Read)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ODataPage>, ProblemHttpResult>> HandleAsync(
        ICompanyScope companyScope, ISender sender, HttpRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ListCustomersODataRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                QueriesSpend = ODataPropertyReferences.ReadsAny<CustomerODataResponse>(request, ODataOptions, SpendMembers)
            },
            cancellationToken);

        return await result.ToODataHttpResultAsync(request, ODataOptions, cancellationToken);
    }
}
