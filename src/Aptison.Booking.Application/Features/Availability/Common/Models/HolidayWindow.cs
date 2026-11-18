namespace Aptison.Booking.Application.Features.Availability.Common.Models;

internal readonly record struct HolidayWindow(
    DateOnly StartsOn, DateOnly EndsOn, bool IsClosed, TimeOnly? OverrideOpen, TimeOnly? OverrideClose);
