using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>The booking page's first question for a signed-in customer, by the shop's slug.</summary>
public class GetCurrentUserBookingAccessRequest : IQuery<Result<BookingAccessResponse, HttpError>>
{
    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    public string Slug { get; set; } = null!;
}

public class GetCurrentUserBookingAccessHandler(IUnitOfWork<BookingContext> unitOfWork, IUserContextScope userContext)
    : IQueryHandler<GetCurrentUserBookingAccessRequest, Result<BookingAccessResponse, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();

    public async Task<Result<BookingAccessResponse, HttpError>> HandleAsync(
        GetCurrentUserBookingAccessRequest request, CancellationToken cancellationToken)
    {
        var company = await _companyRepo
            .QuerySelect(
                company => new { company.Id, company.Phone },
                query => query.Where(company => company.Slug == request.Slug && company.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        // No company scope here: the shop is named by its slug, and the block is that shop's row.
        using var _ = userContext.BecomeAllOwners();

        var isBlocked = await BookingAccessReader.IsBlockedByShopAsync(
            unitOfWork, request.CustomerId, company.Id, cancellationToken);

        return new BookingAccessResponse(!isBlocked, company.Phone);
    }
}
