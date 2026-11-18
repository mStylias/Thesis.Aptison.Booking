using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Infrastructure.Persistence;
using Npgsql;

namespace Aptison.Booking.Application.IntegrationTests.Persistence;

/// <summary>
/// The sqlstates a handler turns into a designed refusal, read off a real driver. The deadlock is on
/// advisory locks, not rows: parallel tests hold foreign-key locks on shared rows and join the cycle.
/// </summary>
public class PostgresConstraintReaderTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private const int DeadlockTestLockClass = 9_901;

    [Test]
    public async Task IsDeadlock_WhenPostgresKilledThisTransactionAsTheVictim_ShouldReadItAsADeadlock()
    {
        // Arrange
        var reader = new PostgresConstraintReader();

        await using var first = new NpgsqlConnection(Database.ConnectionString);
        await using var second = new NpgsqlConnection(Database.ConnectionString);
        await first.OpenAsync();
        await second.OpenAsync();

        await using var firstTransaction = await first.BeginTransactionAsync();
        await using var secondTransaction = await second.BeginTransactionAsync();

        await LockAsync(firstTransaction, 1);
        await LockAsync(secondTransaction, 2);

        // Act
        // Each now waits on the lock the other holds. PostgreSQL breaks the cycle after
        // deadlock_timeout by killing one of them, so this takes about a second.
        var crossed = await Task.WhenAll(
            CaptureAsync(() => LockAsync(firstTransaction, 2)),
            CaptureAsync(() => LockAsync(secondTransaction, 1)));

        // Assert
        var victim = crossed.Single(exception => exception is not null)!;
        await Assert.That(reader.IsDeadlock(victim)).IsTrue();

        // 40P01 names no constraint, which is why the handler has to ask twice.
        await Assert.That(reader.IsViolated(victim, "appointments_no_double_booking")).IsFalse();
    }

    private static async Task LockAsync(NpgsqlTransaction transaction, int key)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@class, @key)", transaction.Connection, transaction);

        command.Parameters.AddWithValue("class", DeadlockTestLockClass);
        command.Parameters.AddWithValue("key", key);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Exception?> CaptureAsync(Func<Task> act)
    {
        try
        {
            await act();

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
