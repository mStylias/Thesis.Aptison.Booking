namespace Aptison.Booking.Application.Features.Reviews.Common.Responses;

/// <summary>One of the customer's own reviews, with the visit it is about. <c>VisitDate</c> is shop-local <c>yyyy-MM-dd</c>.</summary>
public record ReviewResponse(
    Guid Id,
    Guid AppointmentId,
    Guid CompanyId,
    string CompanySlug,
    string CompanyName,
    string? CompanyLogoUrl,
    Guid StaffMemberId,
    string StaffDisplayName,
    string VisitDate,
    short Rating,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
