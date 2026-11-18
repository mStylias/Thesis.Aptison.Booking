using Aptison.Booking.Application.Abstractions.Storage;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Reviews.Common;
using Aptison.Booking.Application.Features.Reviews.Common.Readers;
using Aptison.Booking.Application.Features.Reviews.Common.Responses;
using Aptison.Booking.Application.Features.Reviews.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.EntityFrameworkCore.AspNetCore;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reviews.Commands;

/// <summary>
/// The author edits their review, at any time. Merge-patch: an absent member is unchanged, and a
/// <c>null</c> comment clears it. The ratings follow the edit.
/// </summary>
public class UpdateCurrentUserReviewRequest : ICommand<Result<ReviewResponse, HttpError>>
{
    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    [JsonIgnore]
    public Guid Id { get; set; }

    /// <inheritdoc cref="CreateCurrentUserReviewRequest.LanguageCode"/>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    public PatchValue<short> Rating { get; set; }

    public PatchValue<string?> Comment { get; set; }
}

public class UpdateCurrentUserReviewHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    HybridCache cache,
    ILogger<UpdateCurrentUserReviewHandler> logger,
    IOptions<FileStorageOptions> fileStorageOptions)
    : ICommandHandler<UpdateCurrentUserReviewRequest, Result<ReviewResponse, HttpError>>,
        IWithValidation<UpdateCurrentUserReviewRequest, Result<HttpError>>
{
    private readonly IRepository<Review, BookingContext> _reviewRepo = unitOfWork.GetRepository<Review>();

    public Task<Result<HttpError>> ValidateAsync(
        UpdateCurrentUserReviewRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new Validation("The review is invalid.")
            .RequireRating(!request.Rating.IsSet || ReviewRules.IsValidRating(request.Rating.Value), nameof(request.Rating))
            .RequireComment(!request.Comment.IsSet || ReviewRules.IsValidComment(request.Comment.Value), nameof(request.Comment))
            .ToResult());

    public async Task<Result<ReviewResponse, HttpError>> HandleAsync(
        UpdateCurrentUserReviewRequest request, CancellationToken cancellationToken)
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

            if (request.Rating.IsSet)
            {
                review.Rating = request.Rating.Value;
            }

            if (request.Comment.IsSet)
            {
                review.Comment = StringUtils.TrimToNull(request.Comment.Value);
            }

            var saved = await Result.TryAsync(
                () => unitOfWork.SaveChangesAsync(cancellationToken),
                _ => HttpError.Internal(
                    "The review could not be saved. Please try again.", errorCode: ErrorCodes.Reviews.SaveFailed));

            if (saved.IsError)
            {
                return saved.Error!;
            }

            companyId = review.OwnerId;
        }

        await CompanyRatingCache.EvictAsync(cache, companyId, logger, cancellationToken);

        var reviews = await CustomerReviewReader.ReadAsync(
            unitOfWork, userContext, request.CustomerId, query => query.Where(review => review.Id == request.Id),
            request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl, cancellationToken);

        return reviews.Count == 0
            ? HttpError.NotFound("The review was not found.", errorCode: ErrorCodes.Reviews.NotFound)
            : reviews[0];
    }
}
