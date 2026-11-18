using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common;

/// <summary>
/// How long a selection takes. <b>The availability reads and the insert both call this</b> — a slot
/// offered under one definition and refused under another is the worst bug this feature has.
/// </summary>
internal static class BookingDuration
{
    /// <summary>What the customer is quoted: durations only.</summary>
    public static int GetCustomerMinutes(IReadOnlyList<ServiceDuration> services)
    {
        var minutes = 0;

        foreach (var service in services)
        {
            minutes += service.Minutes;
        }

        return minutes;
    }

    /// <summary>
    /// What the calendar blocks. Buffers count only at the ends: two services in the same chair run
    /// straight into each other, so the turnaround between them is not real.
    /// </summary>
    public static int GetBlockedMinutes(IReadOnlyList<ServiceDuration> services) =>
        services[0].BufferBeforeMinutes
        + GetCustomerMinutes(services)
        + services[^1].BufferAfterMinutes;
}
