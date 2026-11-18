using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

/// <summary>The moves staff may make between statuses. Cancelling is its own action, and nothing leaves it.</summary>
internal static class AppointmentStatusRules
{
    /// <summary>What <c>POST /appointments/{id}/status</c> accepts.</summary>
    public static readonly IReadOnlySet<string> SettableStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        AppointmentStatus.Confirmed,
        AppointmentStatus.Completed,
        AppointmentStatus.NoShow
    };

    public static bool IsActive(string status) => AppointmentStatus.ActiveStatuses.Contains(status);

    /// <summary>Complete and no-show only once the start has passed. An undo occupies the slot again.</summary>
    public static bool CanChange(string from, string to, DateTime startsAt, DateTime utcNow) =>
        (from, to) switch
        {
            (AppointmentStatus.Pending, AppointmentStatus.Confirmed) => true,
            (AppointmentStatus.Confirmed, AppointmentStatus.Completed or AppointmentStatus.NoShow) => startsAt <= utcNow,
            (AppointmentStatus.Completed or AppointmentStatus.NoShow, AppointmentStatus.Confirmed) => true,
            _ => false
        };
}
