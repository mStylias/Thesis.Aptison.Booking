namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>
/// The From address of every email, copied from the transport's own setting rather than bound twice:
/// a calendar invitation whose organizer differs from the sender can be refused by Gmail.
/// </summary>
public sealed class EmailSenderOptions
{
    public string Address { get; set; } = string.Empty;
}
