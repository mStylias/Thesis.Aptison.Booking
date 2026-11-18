namespace Aptison.Booking.Application.Abstractions.Analytics;

/// <summary>Appointments by whole days between the shop-local booking date and the appointment date.</summary>
public sealed class LeadTimeFacts
{
    public int LeadDays { get; init; }
    public required string Source { get; init; }
    public required string Status { get; init; }
    public int Appointments { get; init; }
}
