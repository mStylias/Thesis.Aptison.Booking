using Aptison.Booking.Application.Features.Reviews.Common;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reviews.Commands;

/// <summary>
/// The author deletes their review. It stops counting at once, and the visit can be reviewed again
/// while its 30 days last: the one-review index covers live rows only.
/// </summary>
public class DeleteCurrentUserReviewRequest : ICommand<Result<HttpError>>
{
    public Guid CustomerId { get; set; }

    public Guid Id { get; set; }
}

public class DeleteCurrentUserReviewHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    HybridCache cache,
    ILogger<DeleteCurrentUserReviewHandler> logger)
    : ICommandHandler<DeleteCurrentUserReviewRequest, Result<HttpError>>
{
    private readonly IRepository<Review, BookingContext> _reviewRepo = unitOfWork.GetRepository<Review>();

    public async Task<Result<HttpError>> HandleAsync(
        DeleteCurrentUserReviewRequest request, CancellationToken cancellationToken)
    {
        Guid companyId;

        // Reviews are required-owner and this route names no company; the customer filter scopes it.
        using (userContext.BecomeAllOwners())
        {
            var review = await _reviewRepo
                .Query(
                    query => query.Where(row => row.Id == request.Id && row.CustomerId == request.CustomerId),
                    asNoTracking: false)
                .FirstOrDefaultAsync(cancellationToken);

            if (review is null)
            {
                return HttpError.NotFound("The review was not found.", errorCode: ErrorCodes.Reviews.NotFound);
            }

            _reviewRepo.Remove(review);

            var saved = await Result.TryAsync(
                () => unitOfWork.SaveChangesAsync(cancellationToken),
                _ => HttpError.Internal(
                    "The review could not be deleted. Please try again.", errorCode: ErrorCodes.Reviews.SaveFailed));

            if (saved.IsError)
            {
                return saved.Error!;
            }

            companyId = review.OwnerId;
        }

        await CompanyRatingCache.EvictAsync(cache, companyId, logger, cancellationToken);

        return Result.Ok();
    }
}
