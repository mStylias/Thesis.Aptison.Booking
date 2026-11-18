namespace Aptison.Booking.Domain.Entities.Appointments;

/// <summary>
/// The status vocabulary. There is no <c>CHECK</c> constraint, so this type is the only source of
/// truth — never spell a status as a literal.
/// </summary>
public static class AppointmentStatus
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string NoShow = "no_show";

    /// <summary>
    /// The statuses that occupy a staff member's calendar. <b>Changing this set requires a matching
    /// SQL migration</b>: <c>appointments_no_double_booking</c> and the
    /// <c>ix_appointments_*_starts</c> predicates hardcode the list, and a status missing from them
    /// is invisible to the double-booking guard — slots double-book with no error and no log line.
    /// </summary>
    public static readonly IReadOnlySet<string> ActiveStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Confirmed
    };

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Confirmed,
        Completed,
        Cancelled,
        NoShow
    };
}
