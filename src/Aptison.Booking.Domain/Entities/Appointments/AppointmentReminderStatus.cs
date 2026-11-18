namespace Aptison.Booking.Domain.Entities.Appointments;

public static class AppointmentReminderStatus
{
    /// <summary>Claimed and staged in the outbox.</summary>
    public const string Queued = "queued";

    /// <summary>Delivered: the mail server took the email, or at least one device's push service took the push.</summary>
    public const string Sent = "sent";

    /// <summary>Never staged: a §3.2 rule, or nobody to send it to on this channel.</summary>
    public const string Skipped = "skipped";

    /// <summary>Staged, then not sent because the appointment or the customer changed before delivery.</summary>
    public const string Dropped = "dropped";

    /// <summary>The last attempt failed. Final once the outbox stops retrying.</summary>
    public const string Failed = "failed";
}
