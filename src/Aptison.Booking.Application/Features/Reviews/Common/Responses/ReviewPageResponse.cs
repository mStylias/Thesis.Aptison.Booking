namespace Aptison.Booking.Application.Features.Reviews.Common.Responses;

public record ReviewPageResponse(
    IReadOnlyList<ReviewResponse> Items,
    int Page,
    int PageSize,
    bool HasMore);
