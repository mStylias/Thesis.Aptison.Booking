namespace Aptison.Booking.Application.Abstractions.Persistence;

/// <summary>Lets one instance at a time scan for due reminders.</summary>
public interface IReminderScanLock
{
    /// <summary>
    /// False when another instance holds it. Held until the caller's transaction ends, so it must
    /// be called inside one.
    /// </summary>
    Task<bool> TryAcquireAsync(CancellationToken cancellationToken);
}
