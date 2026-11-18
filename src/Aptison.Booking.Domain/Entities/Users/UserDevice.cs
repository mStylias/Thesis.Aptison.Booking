using System.Text.Json;

namespace Aptison.Booking.Domain.Entities.Users;

public class UserDevice : SoftDeletableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Platform { get; set; } = null!;
    public JsonDocument PushData { get; set; } = null!;
    public string? DeviceName { get; set; }
    public string? AppVersion { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
}
