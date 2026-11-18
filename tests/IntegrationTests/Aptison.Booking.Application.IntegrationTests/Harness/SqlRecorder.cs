using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// Records the SQL EF actually sent, so a test can assert on the statement and not only on the rows
/// — the difference between "the filter worked" and "the filter ran in the database". Shared by
/// every test in the run, so each connection keeps its own history: tests run in parallel, and one
/// test's traffic must not push another's statements out.
/// </summary>
public sealed class SqlRecorder : DbCommandInterceptor
{
    private const int CapacityPerConnection = 200;

    private readonly Lock _gate = new();
    private readonly ConditionalWeakTable<DbConnection, List<string>> _commandTextsByConnections = new();
    private readonly ConditionalWeakTable<DbConnection, StrongBox<int>> _commandCountsByConnections = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// The most recent statement sent on <paramref name="connection"/> containing
    /// <paramref name="fragment"/>, ignoring case.
    /// </summary>
    public string GetLastMentioning(string fragment, DbConnection connection)
    {
        lock (_gate)
        {
            if (_commandTextsByConnections.TryGetValue(connection, out var commandTexts))
            {
                for (var index = commandTexts.Count - 1; index >= 0; index--)
                {
                    if (commandTexts[index].Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    {
                        return commandTexts[index];
                    }
                }
            }
        }

        throw new InvalidOperationException($"No recorded SQL on this connection mentions '{fragment}'.");
    }

    /// <summary>How many statements were sent on <paramref name="connection"/>. Not capped like the history.</summary>
    public int GetCommandCount(DbConnection connection)
    {
        lock (_gate)
        {
            return _commandCountsByConnections.TryGetValue(connection, out var count) ? count.Value : 0;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _commandTextsByConnections.Clear();
            _commandCountsByConnections.Clear();
        }
    }

    private void Record(DbCommand command)
    {
        if (command.Connection is null)
        {
            return;
        }

        lock (_gate)
        {
            var commandTexts = _commandTextsByConnections.GetOrCreateValue(command.Connection);
            commandTexts.Add(command.CommandText);
            _commandCountsByConnections.GetOrCreateValue(command.Connection).Value++;

            if (commandTexts.Count > CapacityPerConnection)
            {
                commandTexts.RemoveRange(0, commandTexts.Count - CapacityPerConnection);
            }
        }
    }
}
