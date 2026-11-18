namespace Aptison.Booking.Tests.Common.Time;

/// <summary>A clock pinned to one instant, and movable. Nothing about a test should depend on now.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
