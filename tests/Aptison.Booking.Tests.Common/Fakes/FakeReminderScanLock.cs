using Aptison.Booking.Application.Abstractions.Persistence;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>Set <see cref="IsHeldElsewhere"/> to act as the instance that lost the lock.</summary>
public sealed class FakeReminderScanLock : IReminderScanLock
{
    public bool IsHeldElsewhere { get; set; }

    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken) => Task.FromResult(!IsHeldElsewhere);
}
