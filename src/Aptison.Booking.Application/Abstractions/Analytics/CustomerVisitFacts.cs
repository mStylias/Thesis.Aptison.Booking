namespace Aptison.Booking.Application.Abstractions.Analytics;

/// <summary>
/// One completed visit. <see cref="PreviousVisitOn"/> and <see cref="FirstVisitOn"/> look at the
/// customer's completed visits at the shop on any date and with any staff member.
/// </summary>
public sealed class CustomerVisitFacts
{
    public Guid CustomerId { get; init; }
    public DateOnly LocalDate { get; init; }
    public decimal TotalPrice { get; init; }
    public DateOnly? PreviousVisitOn { get; init; }
    public DateOnly FirstVisitOn { get; init; }
}
