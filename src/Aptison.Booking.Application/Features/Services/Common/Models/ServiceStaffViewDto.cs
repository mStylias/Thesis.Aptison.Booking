namespace Aptison.Booking.Application.Features.Services.Common.Models;

/// <summary>A null own value follows the service's base value.</summary>
public record ServiceStaffViewDto(Guid StaffMemberId, decimal? CustomPrice, int? CustomDurationMinutes);
