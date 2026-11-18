
namespace Aptison.Booking.Application.Features.Staff.Common.Responses;

/// <summary><c>EndsOn</c> is inclusive; the times are null on whole days.</summary>
public record StaffTimeOffResponse(
    Guid Id,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string? StartTime,
    string? EndTime,
    string? Reason);
