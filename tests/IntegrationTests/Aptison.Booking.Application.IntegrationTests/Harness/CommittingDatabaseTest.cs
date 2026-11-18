namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// For what cannot run inside one rolled-back transaction: a race needing two concurrent
/// transactions, and any handler opening a transaction of its own. These commit for real and
/// truncate afterwards, so they are serialized and phased away from the transactional tests —
/// <c>TRUNCATE</c> takes a lock no concurrent reader survives.
/// </summary>
[ClassDataSource<BookingDatabase>(Shared = SharedType.PerTestSession)]
[ParallelGroup(DatabaseTestGroups.Committing)]
[NotInParallel(DatabaseTestGroups.Committing)]
public abstract class CommittingDatabaseTest(BookingDatabase database) : DatabaseTest(database)
{
    protected override bool IsTransactional => false;

    protected override Task CleanUpAsync() => Database.TruncateAsync();

    /// <summary>
    /// A second caller on its own connection. Its writes are real; the truncate at the end of the
    /// test clears them. Dispose it before the test returns, or the truncate waits on its lock.
    /// </summary>
    protected async Task<TestScopeContext> BeginSeparateScopeAsync() =>
        new(await Database.BeginScopeAsync(transactional: false));
}
