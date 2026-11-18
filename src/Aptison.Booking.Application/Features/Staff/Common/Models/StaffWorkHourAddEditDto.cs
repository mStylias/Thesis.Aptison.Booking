namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>ISO <c>DayOfWeek</c>, shop-local <c>HH:mm</c>. A day may have several ranges.</summary>
public record StaffWorkHourAddEditDto
{
    public int DayOfWeek { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
}
