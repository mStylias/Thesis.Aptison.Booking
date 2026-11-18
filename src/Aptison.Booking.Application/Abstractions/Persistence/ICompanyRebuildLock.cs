namespace Aptison.Booking.Application.Abstractions.Persistence;

/// <summary>
/// Serializes availability rebuilds for one company.
/// <para>
/// The rebuild deletes a day range and reinserts it. Two of them running at once — the outbox hot
/// path and the daily roll, or two application instances — would each delete against their own
/// snapshot and then collide on the primary key. Outbox delivery is at-least-once with no ordering,
/// so this is the normal case rather than a rare one.
/// </para>
/// </summary>
public interface ICompanyRebuildLock
{
    /// <summary>
    /// Blocks until the caller's transaction holds the lock; it is released when that transaction
    /// commits or rolls back. Must therefore be called inside one.
    /// </summary>
    Task AcquireAsync(Guid companyId, CancellationToken cancellationToken);
}
