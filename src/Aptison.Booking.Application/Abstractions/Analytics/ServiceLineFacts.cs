namespace Aptison.Booking.Application.Abstractions.Analytics;

/// <summary>Appointment lines per service, by the appointment's status.</summary>
public sealed class ServiceLineFacts
{
    public Guid ServiceId { get; init; }
    public Guid? CategoryId { get; init; }
    public required string Status { get; init; }
    public int Lines { get; init; }

    /// <summary>Sum of <c>price_at_booking</c>.</summary>
    public decimal Revenue { get; init; }
}
