namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>Own values equal to the service's base are null, so a later base change reaches this member.</summary>
public sealed record StaffServiceInput(Guid ServiceId, decimal? CustomPrice, int? CustomDurationMinutes);
