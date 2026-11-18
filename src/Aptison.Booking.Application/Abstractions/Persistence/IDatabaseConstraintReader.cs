namespace Aptison.Booking.Application.Abstractions.Persistence;

/// <summary>
/// Tells a write the database refused apart from the write being broken. A port because Application
/// cannot name Npgsql, and a refused write is how a lost race reaches a handler.
/// </summary>
public interface IDatabaseConstraintReader
{
    /// <summary>
    /// Whether <paramref name="exception"/>, or anything it wraps, is the named constraint
    /// refusing the write.
    /// </summary>
    bool IsViolated(Exception exception, string constraintName);

    /// <summary>
    /// Whether <paramref name="exception"/>, or anything it wraps, is this write being chosen as a
    /// deadlock's victim — the other shape a race lost to a concurrent writer takes.
    /// </summary>
    bool IsDeadlock(Exception exception);
}
