using Aptison.Booking.Application.Abstractions.Persistence;
using Npgsql;

namespace Aptison.Booking.Infrastructure.Persistence;

/// <inheritdoc />
internal sealed class PostgresConstraintReader : IDatabaseConstraintReader
{
    public bool IsViolated(Exception exception, string constraintName) =>
        EnumeratePostgresExceptions(exception)
            .Any(postgres => string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal));

    // 40P01 carries no constraint name, so which write it settles is the caller's to know.
    public bool IsDeadlock(Exception exception) =>
        EnumeratePostgresExceptions(exception)
            .Any(postgres => postgres.SqlState == PostgresErrorCodes.DeadlockDetected);

    private static IEnumerable<PostgresException> EnumeratePostgresExceptions(Exception exception)
    {
        for (var candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is PostgresException postgres)
            {
                yield return postgres;
            }
        }
    }
}
