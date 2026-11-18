namespace Aptison.Booking.Application.Abstractions.Analytics;

/// <summary>Live reviews by the reviewed appointment's date and staff member.</summary>
public sealed class ReviewDailyFacts
{
    public DateOnly LocalDate { get; init; }
    public Guid StaffMemberId { get; init; }
    public int Rating { get; init; }
    public int Reviews { get; init; }
}
