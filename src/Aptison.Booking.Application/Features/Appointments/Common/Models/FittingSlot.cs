namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>One instant a barber could start the whole selection at.</summary>
internal sealed record FittingSlot(DateTime StartsAt, short LocalMinute, Guid StaffMemberId);
