using Aptison.Booking.Application.Abstractions.Persistence;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Reports the constraints named in <see cref="Violated"/> as violated, and every exception as a
/// deadlock once <see cref="Deadlocked"/> is set, whatever was thrown. For unit tests; integration
/// tests register Infrastructure's real Npgsql-reading implementation.
/// </summary>
public sealed class FakeDatabaseConstraintReader : IDatabaseConstraintReader
{
    public HashSet<string> Violated { get; } = [];

    public bool Deadlocked { get; set; }

    public bool IsViolated(Exception exception, string constraintName) => Violated.Contains(constraintName);

    public bool IsDeadlock(Exception exception) => Deadlocked;
}
