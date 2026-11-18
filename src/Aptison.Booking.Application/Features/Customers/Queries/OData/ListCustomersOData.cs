using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Customers.Common.Readers;
using Aptison.Booking.Application.Features.Customers.Common.ResponseOData;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Customers.Queries.OData;

/// <summary>The customers page and its export: everyone with an appointment at the request's company.</summary>
public class ListCustomersODataRequest : IQuery<Result<ISynaxQuery<CustomerODataResponse>, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    /// <summary>Whether the client's <c>$filter</c> or <c>$orderby</c> reads a spend member.</summary>
    [JsonIgnore]
    public bool QueriesSpend { get; set; }
}

public class ListCustomersODataHandler(
    IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller, TimeProvider timeProvider)
    : IQueryHandler<ListCustomersODataRequest, Result<ISynaxQuery<CustomerODataResponse>, HttpError>>
{
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();

    public async Task<Result<ISynaxQuery<CustomerODataResponse>, HttpError>> HandleAsync(
        ListCustomersODataRequest request, CancellationToken cancellationToken)
    {
        // Refused, not ignored: the spend columns are null for this caller, so a filter on them
        // would quietly return rows that only look filtered.
        if (request.QueriesSpend && !CustomerViewParametersReader.Holds(caller, PermissionCodes.Analytics.Read))
        {
            return HttpError.Forbidden(
                "Filtering or sorting by spend needs the analytics permission.",
                errorCode: ErrorCodes.Customers.SpendForbidden);
        }

        var parameters = await CustomerViewParametersReader.ReadAsync(
            _companyRepo, request.CompanyId, caller, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        if (parameters is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        // No ordering, as on the other OData lists: the first $orderby would override a default.
        return Result.Ok<ISynaxQuery<CustomerODataResponse>, HttpError>(
            _userRepo.QueryDto<CustomerODataResponse>(
                query => query.Where(user => user.Appointments.Any()),
                parameters: parameters));
    }
}
