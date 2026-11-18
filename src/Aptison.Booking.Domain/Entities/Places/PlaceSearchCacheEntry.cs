using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Places;

/// <summary>
/// One provider answer, held so an identical search costs nothing. Durable and shared by every
/// instance because a place does not move and every miss is billed.
/// </summary>
public class PlaceSearchCacheEntry : IEntity
{
    /// <summary>
    /// A SHA-256 of the search — text, language, coarse bias cell and policy version. Hashed
    /// rather than stored plain: what someone typed is a location hint about a real person.
    /// </summary>
    public string CacheKey { get; set; } = null!;

    /// <summary>The composed suggestions as JSON. An empty array is a cached "nothing matched".</summary>
    public string Suggestions { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}
