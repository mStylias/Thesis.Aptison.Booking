namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>A null own value, or one equal to the base, follows the service's base value.</summary>
public record StaffServiceAddEditDto
{
    public Guid ServiceId { get; init; }
    public decimal? CustomPrice { get; init; }
    public int? CustomDurationMinutes { get; init; }
}
