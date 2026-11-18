namespace Aptison.Booking.Domain.Entities.Appointments;

/// <summary>Who booked an appointment. No <c>CHECK</c> constraint, so never spell one as a literal.</summary>
public static class AppointmentSource
{
    /// <summary>The customer, in the app.</summary>
    public const string Online = "online";

    /// <summary>The shop, for a customer.</summary>
    public const string Staff = "staff";
}
