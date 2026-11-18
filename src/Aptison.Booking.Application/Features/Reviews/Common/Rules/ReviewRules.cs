using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Features.Reviews.Common.Rules;

internal static class ReviewRules
{
    public const short MinRating = 1;
    public const short MaxRating = 5;
    public const int MaxCommentLength = 1000;

    public static bool IsValidRating(short? rating) => rating is >= MinRating and <= MaxRating;

    public static bool IsValidComment(string? comment) =>
        StringUtils.TrimToNull(comment) is not { } trimmed || trimmed.Length <= MaxCommentLength;

    public static Validation RequireRating(this Validation validation, bool isValid, string field) =>
        validation.Require(
            isValid, ErrorCodes.Reviews.InvalidRating, $"The rating must be {MinRating} to {MaxRating} whole stars.", field);

    public static Validation RequireComment(this Validation validation, bool isValid, string field) =>
        validation.Require(
            isValid, ErrorCodes.Reviews.InvalidComment, $"The comment must be at most {MaxCommentLength} characters.", field);
}
