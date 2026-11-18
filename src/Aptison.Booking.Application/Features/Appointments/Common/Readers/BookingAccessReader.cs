using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// Whether a shop has blocked a customer from booking online. The callers have no company scope,
/// so the read must run under <c>BecomeAllOwners</c>: otherwise every shop block is invisible.
/// </summary>
internal static class BookingAccessReader
{
    public static Task<bool> IsBlockedByShopAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid customerId, Guid companyId, CancellationToken cancellationToken) =>
        unitOfWork.GetRepository<UserBan>()
            .Query(query => query.Where(ban =>
                ban.UserId == customerId && ban.OwnerId == companyId && ban.LiftedAt == null))
            .AnyAsync(cancellationToken);

    /// <summary>Neutral on purpose: the reason is the shop's and never reaches the customer.</summary>
    public static HttpError CreateBlockedByShopError() =>
        HttpError.Forbidden(
            "This barbershop takes your bookings by phone. Please call them to book.",
            errorCode: ErrorCodes.Appointments.CustomerBlockedByShop);
}
