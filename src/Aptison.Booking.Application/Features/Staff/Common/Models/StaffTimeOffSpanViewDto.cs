
namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>Both days inclusive.</summary>
public record StaffTimeOffSpanViewDto(DateOnly StartsOn, DateOnly EndsOn);
