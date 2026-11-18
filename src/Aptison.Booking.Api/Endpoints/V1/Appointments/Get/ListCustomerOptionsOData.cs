using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Domain.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.OData.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Appointments.Get;

/// <summary>
/// The shop's customers, paged and searchable: the calendar's filter and the drawer's customer
/// picker. An unquoted OData search word takes letters only, so a term with digits or <c>@</c> must
/// be a quoted phrase: <c>$search="6977"</c>. Phones are stored as E.164, so digits without spaces match.
/// </summary>
public class ListCustomerOptionsOData : IVersionedEndpoint
{
    private static readonly SynaxODataOptions ODataOptions = new SynaxODataOptions
    {
        AllowedOrderByProperties = { "displayName", "lastAppointmentDate" }
    }.Searchable<CustomerOptionODataResponse>(
        customer => customer.NameNormalized, customer => customer.Email, customer => customer.PhoneNumber);

    public string Tag => "Appointments";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/customers/odata", HandleAsync)
            .RequirePermission(PermissionCodes.Appointments.ReadAll)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<ODataPage>, ProblemHttpResult>> HandleAsync(
        ISender sender, HttpRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ListCustomerOptionsODataRequest(), cancellationToken);
        return await result.ToODataHttpResultAsync(request, ODataOptions, cancellationToken);
    }
}
