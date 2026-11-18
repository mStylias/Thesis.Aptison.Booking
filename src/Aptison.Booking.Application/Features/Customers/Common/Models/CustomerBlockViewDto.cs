namespace Aptison.Booking.Application.Features.Customers.Common.Models;

public record CustomerBlockViewDto
{
    public string Reason { get; init; } = null!;
    public DateTimeOffset BlockedAt { get; init; }
    public string BlockedByDisplayName { get; init; } = null!;
}
