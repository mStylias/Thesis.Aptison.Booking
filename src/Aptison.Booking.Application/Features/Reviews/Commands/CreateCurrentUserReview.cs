using Aptison.Booking.Application.Abstractions.Storage;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
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
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reviews.Commands;

/// <summary>
/// The customer rates a visit the shop marked completed, up to 30 days after it. Published at once,
/// and counted toward the shop's rating and the rating of the barber who did the visit. A ban
/// does not stop it: a ban stops new bookings only.
/// </summary>
public class CreateCurrentUserReviewRequest : ICommand<Result<ReviewResponse, HttpError>>
{
    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    [JsonIgnore]
    public Guid AppointmentId { get; set; }

    /// <summary>The language of the shop name in the response.</summary>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary>1 to 5 whole stars.</summary>
    public short? Rating { get; set; }

    public string? Comment { get; set; }
}

public class CreateCurrentUserReviewHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IDatabaseConstraintReader constraintReader,
    HybridCache cache,
    TimeProvider timeProvider,
    ILogger<CreateCurrentUserReviewHandler> logger,
    IOptions<FileStorageOptions> fileStorageOptions)
    : ICommandHandler<CreateCurrentUserReviewRequest, Result<ReviewResponse, HttpError>>,
        IWithValidation<CreateCurrentUserReviewRequest, Result<HttpError>>
{
    /// <summary>The partial unique index that holds one live review per appointment.</summary>
    private const string OneReviewPerAppointment = "ux_reviews_appointment";

    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<Review, BookingContext> _reviewRepo = unitOfWork.GetRepository<Review>();

    public Task<Result<HttpError>> ValidateAsync(
        CreateCurrentUserReviewRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new Validation("The review is invalid.")
            .RequireRating(ReviewRules.IsValidRating(request.Rating), nameof(request.Rating))
            .RequireComment(ReviewRules.IsValidComment(request.Comment), nameof(request.Comment))
            .ToResult());

    public async Task<Result<ReviewResponse, HttpError>> HandleAsync(
        CreateCurrentUserReviewRequest request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var reviewId = Guid.CreateVersion7();

        Guid companyId;

        // Appointments and reviews are required-owner and this route names no company; the
        // customer filter scopes the read, and the insert carries its owner explicitly.
        using (userContext.BecomeAllOwners())
        {
            var appointment = await _appointmentRepo
                .QuerySelect(
                    row => new { row.OwnerId, row.StaffMemberId, row.EndsAt, row.Status, HasReview = row.Review != null },
                    query => query.Where(row => row.Id == request.AppointmentId && row.CustomerId == request.CustomerId))
                .FirstOrDefaultAsync(cancellationToken);

            if (appointment is null)
            {
                return HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound);
            }

            if (appointment.Status != AppointmentStatus.Completed)
            {
                return HttpError.Conflict(
                    "Only a visit the barbershop marked completed can be reviewed.",
                    errorCode: ErrorCodes.Reviews.NotReviewable);
            }

            if (appointment.HasReview)
            {
                return HttpError.Conflict(
                    "You have already reviewed this visit. You can edit that review instead.",
                    errorCode: ErrorCodes.Reviews.AlreadyReviewed);
            }

            if (!CustomerAppointmentRules.CanReview(appointment.Status, appointment.EndsAt, hasReview: false, utcNow))
            {
                return HttpError.Conflict(
                    $"A visit can be reviewed up to {CustomerAppointmentRules.ReviewWindow.Days} days after it.",
                    errorCode: ErrorCodes.Reviews.WindowClosed);
            }

            companyId = appointment.OwnerId;

            var saved = await SaveAsync(
                new Review
                {
                    Id = reviewId,
                    OwnerId = appointment.OwnerId,
                    AppointmentId = request.AppointmentId,
                    CustomerId = request.CustomerId,
                    StaffMemberId = appointment.StaffMemberId,
                    Rating = request.Rating!.Value,
                    Comment = StringUtils.TrimToNull(request.Comment),
                    IsPublished = true
                },
                cancellationToken);

            if (saved.IsError)
            {
                return saved.Error!;
            }
        }

        await CompanyRatingCache.EvictAsync(cache, companyId, logger, cancellationToken);

        var reviews = await CustomerReviewReader.ReadAsync(
            unitOfWork, userContext, request.CustomerId, query => query.Where(review => review.Id == reviewId),
            request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl, cancellationToken);

        return reviews.Count == 0
            ? HttpError.NotFound("The review was not found.", errorCode: ErrorCodes.Reviews.NotFound)
            : reviews[0];
    }

    private async Task<Result<HttpError>> SaveAsync(Review review, CancellationToken cancellationToken)
    {
        try
        {
            await _reviewRepo.AddAsync(review, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, OneReviewPerAppointment))
        {
            return HttpError.Conflict(
                "You have already reviewed this visit. You can edit that review instead.",
                errorCode: ErrorCodes.Reviews.AlreadyReviewed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The review could not be saved. Please try again.", errorCode: ErrorCodes.Reviews.SaveFailed);
        }
    }
}
