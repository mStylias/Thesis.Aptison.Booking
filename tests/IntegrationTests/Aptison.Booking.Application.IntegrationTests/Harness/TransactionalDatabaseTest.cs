namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// The default. One connection, one transaction, rolled back when the test ends — including what a
/// handler committed, since its <c>SaveChangesAsync</c> joins that transaction rather than opening
/// its own. One connection per test, so these run in parallel.
/// </summary>
[ClassDataSource<BookingDatabase>(Shared = SharedType.PerTestSession)]
[ParallelGroup(DatabaseTestGroups.Transactional)]
public abstract class TransactionalDatabaseTest(BookingDatabase database) : DatabaseTest(database)
{
    protected override bool IsTransactional => true;
}
