namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>The instants a staff write stores. <c>BlockedFrom</c> is always <c>StartsAt</c>.</summary>
internal sealed record AppointmentWindow(DateTime StartsAt, DateTime EndsAt, bool DurationOverridden)
{
    /// <summary>The buffers widen the far end only, as the customer booking does.</summary>
    public DateTime GetBlockedUntil(PlannedLines lines) =>
        EndsAt.AddMinutes(lines.BufferBeforeMinutes + lines.BufferAfterMinutes);
}
