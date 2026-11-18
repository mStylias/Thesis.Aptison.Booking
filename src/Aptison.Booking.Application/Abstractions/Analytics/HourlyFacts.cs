namespace Aptison.Booking.Application.Abstractions.Analytics;

/// <summary>Appointments by the shop-local weekday (ISO, 1 = Monday) and hour they start.</summary>
public sealed class HourlyFacts
{
    public int IsoWeekday { get; init; }
    public int Hour { get; init; }
    public required string Status { get; init; }
    public int Appointments { get; init; }
}
