namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Reviews
    {
        /// <summary>Also what someone else's review answers, so the id is never confirmed.</summary>
        public const string NotFound = "REVIEW_NOT_FOUND";

        public const string InvalidRating = "REVIEW_INVALID_RATING";
        public const string InvalidComment = "REVIEW_INVALID_COMMENT";

        /// <summary>The shop has not marked the visit completed.</summary>
        public const string NotReviewable = "REVIEW_NOT_REVIEWABLE";

        /// <summary>More than 30 days after the visit.</summary>
        public const string WindowClosed = "REVIEW_WINDOW_CLOSED";

        public const string AlreadyReviewed = "REVIEW_ALREADY_EXISTS";
        public const string FetchFailed = "REVIEW_FETCH_FAILED";
        public const string SaveFailed = "REVIEW_SAVE_FAILED";
    }
}
