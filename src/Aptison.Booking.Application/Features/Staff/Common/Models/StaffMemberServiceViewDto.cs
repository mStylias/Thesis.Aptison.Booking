
namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>A null own value follows the service's base value.</summary>
public record StaffMemberServiceViewDto(Guid ServiceId, decimal? CustomPrice, int? CustomDurationMinutes);
