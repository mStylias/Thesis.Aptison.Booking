namespace Aptison.Booking.Domain.Entities.Appointments;

/// <summary>Why a reminder was skipped or dropped. Also the <c>reason</c> tag on the reminder metrics.</summary>
public static class AppointmentReminderReason
{
    public const string TooSoonAfterBooking = "too-soon-after-booking";
    public const string Superseded = "superseded";
    public const string NoEmail = "no-email";
    public const string EmailRemindersOff = "email-reminders-off";
    public const string NoDevices = "no-devices";
    public const string PushNotConfigured = "push-not-configured";
    public const string AppointmentInactive = "appointment-inactive";
    public const string TimeChanged = "time-changed";
    public const string Started = "started";
    public const string DeviceGone = "device-gone";
}
