using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Analytics.Common;

/// <summary>Which statuses each kind of number counts. Every chart reads its rule from here.</summary>
internal static class AnalyticsStatuses
{
    /// <summary>The order statuses appear in on an axis or as series.</summary>
    public static readonly IReadOnlyList<string> Ordered =
    [
        AppointmentStatus.Completed,
        AppointmentStatus.Confirmed,
        AppointmentStatus.Pending,
        AppointmentStatus.Cancelled,
        AppointmentStatus.NoShow
    ];

    public static bool IsRevenue(string status) => status == AppointmentStatus.Completed;

    /// <summary>Demand and utilization: every booking not called off, a no-show included since it held the chair.</summary>
    public static bool IsBooked(string status) => status != AppointmentStatus.Cancelled;

    /// <summary>Lost-rate denominator: outcomes that are settled, so upcoming bookings do not dilute it.</summary>
    public static bool IsResolved(string status) =>
        status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow;

    public static bool IsUpcoming(string status) => AppointmentStatus.ActiveStatuses.Contains(status);
}
