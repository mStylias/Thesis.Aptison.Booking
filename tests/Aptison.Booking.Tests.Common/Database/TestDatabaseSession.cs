using System.Data;
using System.Data.Common;
using Npgsql;

namespace Aptison.Booking.Tests.Common.Database;

/// <summary>
/// One test's connection to the database, and the transaction it runs inside. Registered scoped, so
/// every <c>DbContext</c>, repository and handler resolved from a test's scope shares it — which is
/// what lets a rollback here undo everything the test did.
/// </summary>
public sealed class TestDatabaseSession(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    private NpgsqlConnection? _connection;
    private DbTransaction? _transaction;

    public NpgsqlConnection Connection =>
        _connection ?? throw new InvalidOperationException("The session has not been opened.");

    /// <summary>Null when the test commits for real rather than rolling back.</summary>
    public DbTransaction? Transaction => _transaction;

    public async Task OpenAsync(bool transactional, CancellationToken cancellationToken = default)
    {
        // From the data source, because Npgsql type plugins live there: a hand-built connection
        // has no PostGIS and fails on the first Point with "writing Point is not supported".
        _connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (transactional)
        {
            _transaction = await _connection.BeginTransactionAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Runs twice per test — <see cref="TestScope"/> calls it, then the DI scope that created it — so
    /// the second call must do nothing. Npgsql recycles the transaction object with its pooled
    /// connection: disposing it again rolls back whichever test holds that connection by then.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var transaction = _transaction;
        var connection = _connection;
        _transaction = null;
        _connection = null;

        if (transaction is not null)
        {
            // Only while the connection can still carry it. Dropping the connection rolls back on
            // the server anyway, so a broken one needs no rollback and would only throw over it.
            if (connection is { State: ConnectionState.Open })
            {
                await transaction.RollbackAsync();
            }

            await transaction.DisposeAsync();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }
}
