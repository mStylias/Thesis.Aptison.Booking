using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Customers.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>
/// Yes or no, by the whole address, before staff book a new customer. Never a name: the booking
/// form learns who the account is only once the appointment is saved.
/// </summary>
public class FindCustomerAccountRequest : IQuery<Result<CustomerAccountLookupResponse, HttpError>>
{
    public string? Email { get; set; }
}

public class FindCustomerAccountHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<FindCustomerAccountRequest, Result<CustomerAccountLookupResponse, HttpError>>,
        IWithValidation<FindCustomerAccountRequest, Result<string, HttpError>>
{
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();

    public Task<Result<string, HttpError>> ValidateAsync(
        FindCustomerAccountRequest request, CancellationToken cancellationToken)
    {
        var email = StringUtils.TrimToNull(request.Email);

        return Task.FromResult(new Validation("The email address is invalid.")
            .Require(
                email is not null && CustomerContactRules.IsEmailAddress(email),
                ErrorCodes.Customers.InvalidEmail,
                "The email address is not valid.",
                nameof(request.Email))
            .ToResult(email ?? string.Empty));
    }

    public async Task<Result<CustomerAccountLookupResponse, HttpError>> HandleAsync(
        FindCustomerAccountRequest request, CancellationToken cancellationToken)
    {
        var email = this.ValidationData();

        // citext equality: the whole address in any case, never a pattern.
        var hasAccount = await _userRepo
            .Query(query => query.Where(user => user.Email == email && user.OwnerId == null))
            .AnyAsync(cancellationToken);

        return new CustomerAccountLookupResponse(hasAccount);
    }
}
