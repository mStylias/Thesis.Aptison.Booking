namespace Aptison.Booking.Application.Features.Companies.Common.Responses;

/// <summary><c>EndsOn</c> is inclusive; the times are null on a closure.</summary>
public record CompanyHolidayResponse(
    Guid Id,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsClosed,
    string? OpenTime,
    string? CloseTime,
    string? Name);
