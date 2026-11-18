namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>A holiday write once parsed. Both times are null on a closure.</summary>
internal sealed record CompanyHolidayInput(
    DateOnly StartsOn, DateOnly EndsOn, bool IsClosed, TimeOnly? OpenTime, TimeOnly? CloseTime, string? Name);
