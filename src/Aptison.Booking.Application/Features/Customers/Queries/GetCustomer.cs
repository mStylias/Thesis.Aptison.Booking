using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Customers.Common.Readers;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Domain;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Customers.Queries;

/// <summary>
/// One customer of the request's company. Another shop's walk-in, someone who never booked here and
/// a walk-in merged into another record all answer not found.
/// </summary>
public class GetCustomerRequest : IQuery<Result<CustomerResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public Guid CustomerId { get; set; }
}

public class GetCustomerHandler(
    IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller, TimeProvider timeProvider)
    : IQueryHandler<GetCustomerRequest, Result<CustomerResponse, HttpError>>
{
    public async Task<Result<CustomerResponse, HttpError>> HandleAsync(
        GetCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await CustomerViewReader.ReadAsync(
            unitOfWork, caller, request.CompanyId, request.CustomerId, timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return customer is null ? CustomerViewReader.CreateNotFoundError() : customer;
    }
}
