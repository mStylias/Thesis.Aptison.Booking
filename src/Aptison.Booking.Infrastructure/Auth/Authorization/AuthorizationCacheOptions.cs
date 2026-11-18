namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// Settings for the permission cache, bound from the <c>Authorization</c> configuration section.
/// </summary>
public sealed class AuthorizationCacheOptions
{
    public const string SectionName = "Authorization";

    /// <summary>Namespace for every key this cache owns.</summary>
    public string KeyPrefix { get; set; } = "auth:perm";

    /// <summary>
    /// How long an idle user's cached grants survive, or <c>null</c> to keep them forever. This is
    /// a memory bound, not a freshness one — freshness is the invalidator's job, and an expired
    /// entry rebuilds from Postgres fresher than what it replaced.
    /// </summary>
    public TimeSpan? UserEntryExpiry { get; set; }

    /// <summary>
    /// Builds the role map at startup, removing the cold-start stampede where every concurrent
    /// first request rebuilds it independently.
    /// </summary>
    public bool WarmOnStartup { get; set; } = true;
}
