namespace Aptison.Booking.Application.Features.Reviews.Common.Models;

internal sealed record CustomerReviewDto(
    Guid Id,
    Guid AppointmentId,
    Guid CompanyId,
    Guid StaffMemberId,
    DateOnly VisitDate,
    short Rating,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
