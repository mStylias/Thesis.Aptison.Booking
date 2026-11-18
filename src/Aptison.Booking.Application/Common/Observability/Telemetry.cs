namespace Aptison.Booking.Application.Common.Observability;

/// <summary>Shared OpenTelemetry identifiers for the application.</summary>
public static class Telemetry
{
    public const string ServiceName = "aptison-booking-api";

    /// <summary>Must be passed to <c>AddMeter</c> in the host, or nothing it records is exported.</summary>
    public const string PlaceSearchMeterName = "Aptison.Booking.PlaceSearch";

    /// <inheritdoc cref="PlaceSearchMeterName"/>
    public const string RemindersMeterName = "Aptison.Booking.Reminders";
}
