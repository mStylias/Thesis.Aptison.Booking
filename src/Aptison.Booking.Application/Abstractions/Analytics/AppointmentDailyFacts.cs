namespace Aptison.Booking.Application.Abstractions.Analytics;

public sealed class AppointmentDailyFacts
{
    public DateOnly LocalDate { get; init; }
    public Guid StaffMemberId { get; init; }
    public required string Status { get; init; }
    public required string Source { get; init; }
    public bool CancelledByCustomer { get; init; }
    public int Appointments { get; init; }

    /// <summary>Sum of <c>total_price</c>, whatever the status.</summary>
    public decimal Revenue { get; init; }

    /// <summary>Customer-visible minutes, buffers excluded.</summary>
    public int Minutes { get; init; }
}
