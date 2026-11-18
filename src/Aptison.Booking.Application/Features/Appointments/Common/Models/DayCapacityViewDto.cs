namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>One shop-local day's start times for a selection. <c>FreeSlots / TotalSlots</c> is the share still free.</summary>
public record DayCapacityViewDto(string Date, int FreeSlots, int TotalSlots);
