namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <param name="DayOfWeek">As stored: <c>0</c> = Sunday, as <see cref="System.DayOfWeek"/> counts.</param>
public sealed record StaffWorkHourInput(short DayOfWeek, TimeOnly Start, TimeOnly End);
