using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Infrastructure.Persistence;
using Npgsql;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// Infrastructure's real reader, with a note of every exception it was asked about. A handler that
/// turns a database refusal into a designed outcome does it inside an exception filter, where
/// nothing else can see what it caught — this is how a test asks.
/// </summary>
public sealed class RecordingConstraintReader : IDatabaseConstraintReader
{
    private readonly PostgresConstraintReader _inner = new();
    private readonly Lock _gate = new();
    private readonly List<Exception> _seen = [];

    public bool IsViolated(Exception exception, string constraintName)
    {
        Record(exception);

        return _inner.IsViolated(exception, constraintName);
    }

    public bool IsDeadlock(Exception exception)
    {
        Record(exception);

        return _inner.IsDeadlock(exception);
    }

    /// <summary>Each recorded failure as <c>sqlstate/constraint</c>, or the exception's type name.</summary>
    public IReadOnlyList<string> Describe()
    {
        lock (_gate)
        {
            return [.. _seen.Select(DescribeOne)];
        }
    }

    // By instance: the handler asks about one failure twice, and that is one failure.
    private void Record(Exception exception)
    {
        lock (_gate)
        {
            if (!_seen.Any(seen => ReferenceEquals(seen, exception)))
            {
                _seen.Add(exception);
            }
        }
    }

    private static string DescribeOne(Exception exception)
    {
        for (var candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is PostgresException postgres)
            {
                return $"{postgres.SqlState}/{postgres.ConstraintName}";
            }
        }

        return exception.GetType().Name;
    }
}
