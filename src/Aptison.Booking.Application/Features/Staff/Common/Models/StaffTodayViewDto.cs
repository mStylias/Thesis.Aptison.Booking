
namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>Empty on a day off: the member's hours inside the shop's, holidays applied.</summary>
public record StaffTodayViewDto(IReadOnlyList<StaffWindowViewDto> Windows);
