
namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>Every live service, inactive ones included, in category order then by name.</summary>
public record StaffServiceOptionViewDto(
    Guid Id,
    string Name,
    Guid? CategoryId,
    string? CategoryName,
    decimal Price,
    int DurationMinutes,
    bool IsActive);
