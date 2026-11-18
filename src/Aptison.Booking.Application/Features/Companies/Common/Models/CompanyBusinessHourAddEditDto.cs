namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>ISO <c>DayOfWeek</c>, shop-local <c>HH:mm</c>. A day may have several ranges.</summary>
public record CompanyBusinessHourAddEditDto
{
    public int DayOfWeek { get; init; }
    public string? OpenTime { get; init; }
    public string? CloseTime { get; init; }
}
