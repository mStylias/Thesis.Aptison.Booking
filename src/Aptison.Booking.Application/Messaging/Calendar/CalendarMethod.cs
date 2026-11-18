namespace Aptison.Booking.Application.Messaging.Calendar;

public enum CalendarMethod
{
    /// <summary>Adds the event, or updates the one with the same UID and a lower SEQUENCE.</summary>
    Request,

    /// <summary>Removes the event, or marks it cancelled.</summary>
    Cancel
}
