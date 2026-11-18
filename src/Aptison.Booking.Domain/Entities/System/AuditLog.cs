using System.Net;
using System.Text.Json;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.System;

// Optional owner: a platform-level action belongs to no company and stays visible everywhere,
// while company-scoped entries are filtered to their own company.
public class AuditLog : IEntity, IHasOptionalOwner<Guid?>
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
    public string Action { get; set; } = null!;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? OwnerId { get; set; }
    public Company? Owner { get; set; }
    public IPAddress? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public JsonDocument? Data { get; set; }
}
