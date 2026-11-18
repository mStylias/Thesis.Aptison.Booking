namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public record AppointmentReviewViewDto(
    Guid Id,
    short Rating,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
