namespace Aptison.Booking.Application.Features.Services.Common.Models;

public sealed record ServiceStaffInput(Guid StaffMemberId, decimal? CustomPrice, int? CustomDurationMinutes);
