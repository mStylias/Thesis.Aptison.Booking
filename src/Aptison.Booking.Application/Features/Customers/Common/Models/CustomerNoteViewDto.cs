namespace Aptison.Booking.Application.Features.Customers.Common.Models;

public record CustomerNoteViewDto
{
    public string Text { get; init; } = null!;
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedByDisplayName { get; init; }
}
