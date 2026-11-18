using Aptison.Booking.Application.Abstractions.Storage;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Reviews.Common.Readers;
using Aptison.Booking.Application.Features.Reviews.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reviews.Queries;

/// <summary>The caller's reviews at every shop, newest first.</summary>
public class ListCurrentUserReviewsRequest : IQuery<Result<ReviewPageResponse, HttpError>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    /// <summary>From 1. Out-of-range values are clamped rather than refused.</summary>
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    /// <summary>The language of the shop names. Falls back to whichever each shop has.</summary>
    public string? LanguageCode { get; set; }
}

public class ListCurrentUserReviewsHandler(IUnitOfWork<BookingContext> unitOfWork, IUserContextScope userContext,
    IOptions<FileStorageOptions> fileStorageOptions)
    : IQueryHandler<ListCurrentUserReviewsRequest, Result<ReviewPageResponse, HttpError>>
{
    public async Task<Result<ReviewPageResponse, HttpError>> HandleAsync(
        ListCurrentUserReviewsRequest request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = Math.Clamp(
            request.PageSize ?? ListCurrentUserReviewsRequest.DefaultPageSize, 1, ListCurrentUserReviewsRequest.MaxPageSize);

        // One past the page, to learn whether another follows without counting them all.
        var read = await Result.TryAsync(
            () => CustomerReviewReader.ReadAsync(
                unitOfWork,
                userContext,
                request.CustomerId,
                query => query
                    .OrderByDescending(review => review.CreatedAt)
                    .ThenByDescending(review => review.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize + 1),
                request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl,
                cancellationToken),
            _ => HttpError.Internal("Failed to fetch your reviews.", errorCode: ErrorCodes.Reviews.FetchFailed));

        if (read.IsError)
        {
            return read.Error!;
        }

        var reviews = read.Data!;

        return new ReviewPageResponse([.. reviews.Take(pageSize)], page, pageSize, reviews.Count > pageSize);
    }
}
