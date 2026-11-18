namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>A time off as stored: UTC instants, plus the local days the calendar rebuild covers.</summary>
internal sealed record StaffTimeOffInput(
    DateTime StartsAt, DateTime EndsAt, string? Reason, DateOnly StartsOn, DateOnly EndsOn);
