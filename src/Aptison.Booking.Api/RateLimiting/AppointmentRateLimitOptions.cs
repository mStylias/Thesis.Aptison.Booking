namespace Aptison.Booking.Api.RateLimiting;

/// <summary>
/// The per-customer ceiling on booking attempts. A booking creates a real obligation for a real
/// barbershop, so this is the floor under abuse rather than a nicety.
/// </summary>
public sealed class AppointmentRateLimitOptions
{
    public const string SectionName = "AppointmentRateLimit";

    public int BookingsPerMinute { get; set; } = 5;
}
