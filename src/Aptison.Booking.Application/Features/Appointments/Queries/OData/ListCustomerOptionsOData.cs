using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries.OData;

/// <summary>
/// The calendar's customer filter: everyone with an appointment at the request's company. The
/// ownership filter on <c>Appointments</c> decides who that is; the one on users only hides other
/// shops' walk-ins, which never have one here.
/// </summary>
public class ListCustomerOptionsODataRequest : IQuery<Result<ISynaxQuery<CustomerOptionODataResponse>, HttpError>>;

public class ListCustomerOptionsODataHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<ListCustomerOptionsODataRequest, Result<ISynaxQuery<CustomerOptionODataResponse>, HttpError>>
{
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();

    public Task<Result<ISynaxQuery<CustomerOptionODataResponse>, HttpError>> HandleAsync(
        ListCustomerOptionsODataRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(
            Result.Ok<ISynaxQuery<CustomerOptionODataResponse>, HttpError>(
                _userRepo.QueryDto<CustomerOptionODataResponse>(query => query
                    .Where(user => user.Appointments.Any()))));
}
