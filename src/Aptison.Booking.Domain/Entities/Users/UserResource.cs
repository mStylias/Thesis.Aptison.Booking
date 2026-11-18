using System.Text.Json;
using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Users;

/// <summary>
/// An arbitrary JSON value a user stores for themselves — a saved filter, a layout, a draft.
/// The platform never reads <see cref="Value"/>; only the client that wrote it knows its shape.
/// </summary>
public class UserResource : SoftDeletableEntity, IHasOptionalOwner<Guid?>
{
    /// <summary>
    /// Null for the personal rows the <c>/me/resources</c> endpoints write, since those endpoints have
    /// no company scope. Optional rather than required so those rows are reachable at platform scope.
    /// </summary>
    public Guid? OwnerId { get; set; }

    public Company? Owner { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public JsonDocument Value { get; set; } = null!;

    /// <summary>Stored and returned; no read path widens on it yet.</summary>
    public bool IsShared { get; set; }
}
