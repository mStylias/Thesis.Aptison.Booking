namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>The lines a staff write stores, in the order given, and what they cost the barber's calendar.</summary>
internal sealed record PlannedLines(
    IReadOnlyList<CandidateOffering> Lines,
    int CustomerMinutes,
    int BufferBeforeMinutes,
    int BufferAfterMinutes)
{
    public decimal TotalPrice => Lines.Sum(line => line.Price);
}
