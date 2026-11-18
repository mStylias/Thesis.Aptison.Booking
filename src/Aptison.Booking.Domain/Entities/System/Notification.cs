using System.Text.Json;

namespace Aptison.Booking.Domain.Entities.System;

public class Notification : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public JsonDocument? Data { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
